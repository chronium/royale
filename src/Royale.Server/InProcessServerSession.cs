using Royale.Protocol;
using Royale.Content;

namespace Royale.Server;

public sealed class InProcessServerSession : IDisposable
{
    private readonly HeadlessServerSimulation simulation;
    private readonly Dictionary<ServerConnectionId, InProcessClientState> clients = [];
    private readonly Dictionary<ServerPlayerId, BotInputState> botInputs = [];
    private uint nextConnectionId = 1;
    private bool disposed;

    private InProcessServerSession(HeadlessServerSimulation simulation)
    {
        this.simulation = simulation;
    }

    public ulong CurrentTick => simulation.CurrentTick;

    public string MapId => simulation.MapId;

    public int ActivePlayerCount => simulation.ActivePlayerCount;

    public int HumanPlayerCount => simulation.HumanPlayerCount;

    public int BotPlayerCount => simulation.BotPlayerCount;

    public int ConnectedClientCount => clients.Count;

    public int LivingPlayerCount => simulation.LivingPlayerCount;

    public MatchPhase MatchPhase => simulation.MatchState.Phase;

    public MatchStartSettings MatchStartSettings => simulation.MatchStartSettings;

    public int QueuedInputCommandCount =>
        clients.Values.Sum(client => client.InputCommands.Count) +
        botInputs.Values.Count(input => input.PendingCommand.HasValue);

    public bool IsDisposed => disposed;

    public bool IsSimulationDisposed => simulation.IsDisposed;

    public static InProcessServerSession Create(
        string mapId,
        MatchStartSettings? matchStartSettings = null) =>
        new(HeadlessServerSimulation.Create(mapId, matchStartSettings));

    public static InProcessServerSession Create(
        GameMap map,
        MatchStartSettings? matchStartSettings = null) =>
        new(HeadlessServerSimulation.Create(map, matchStartSettings));

    public IReadOnlyList<ServerPlayerDebugState> GetPlayerDebugStates(
        IReadOnlyDictionary<ServerPlayerId, int>? peerIdsByPlayerId = null)
    {
        ThrowIfDisposed();

        return simulation.Players.Values
            .OrderBy(player => player.PlayerId.Value)
            .Select(player => CreatePlayerDebugState(
                player,
                peerIdsByPlayerId,
                clients.TryGetValue(
                    player.ConnectionId ?? default,
                    out InProcessClientState? client)
                    ? client.InputCommands.Count
                    : botInputs.TryGetValue(player.PlayerId, out BotInputState? botInput) &&
                        botInput.PendingCommand.HasValue
                        ? 1
                        : 0))
            .ToArray();
    }

    public InProcessClientConnection ConnectClient()
    {
        ThrowIfDisposed();

        ServerConnectionId connectionId = new(nextConnectionId++);
        AuthoritativePlayerState player = simulation.AddHumanPlayer(connectionId);
        var client = new InProcessClientConnection(connectionId, player.PlayerId);
        var state = new InProcessClientState(connectionId, player.PlayerId);

        state.Snapshots.Enqueue(simulation.CreateSnapshot(player.PlayerId));
        clients.Add(connectionId, state);

        return client;
    }

    public ServerPlayerId AddBot()
    {
        ThrowIfDisposed();
        ServerPlayerId playerId = simulation.AddBotPlayer().PlayerId;
        botInputs.Add(playerId, new BotInputState());
        return playerId;
    }

    public bool TryRemoveBot(ServerPlayerId playerId)
    {
        ThrowIfDisposed();

        if (!simulation.TryGetPlayer(playerId, out AuthoritativePlayerState? player) ||
            player is null ||
            player.Kind != ServerPlayerKind.Bot)
        {
            return false;
        }

        bool removed = simulation.RemovePlayer(playerId);
        if (removed)
            botInputs.Remove(playerId);

        return removed;
    }

    public bool TrySubmitBotInput(ServerPlayerId playerId, BotInputIntent intent)
    {
        ThrowIfDisposed();

        if (!simulation.TryGetPlayer(playerId, out AuthoritativePlayerState? player) ||
            player is null ||
            player.Kind != ServerPlayerKind.Bot ||
            !botInputs.TryGetValue(playerId, out BotInputState? inputState) ||
            inputState.PendingCommand.HasValue)
        {
            return false;
        }

        var validationCommand = new PlayerInputCommand(
            Sequence: 0,
            ClientTick: 0,
            intent.Move,
            intent.YawRadians,
            intent.PitchRadians,
            intent.Buttons);

        if (!PlayerInputCommandValidation.IsValid(validationCommand))
            return false;

        inputState.PendingCommand = validationCommand with
        {
            Sequence = inputState.NextSequence,
            ClientTick = CurrentTick >= uint.MaxValue ? uint.MaxValue : (uint)CurrentTick,
        };
        inputState.NextSequence = unchecked(inputState.NextSequence + 1);
        return true;
    }

    public bool TryEnqueueInputCommand(InProcessClientConnection client, PlayerInputCommand command)
    {
        InProcessClientState state = GetClientState(client);

        if (!PlayerInputCommandValidation.IsValid(command))
            return false;

        state.InputCommands.Enqueue(command);
        return true;
    }

    public bool TryDequeueSnapshot(InProcessClientConnection client, out ServerSnapshot? snapshot)
    {
        InProcessClientState state = GetClientState(client);
        return state.Snapshots.TryDequeue(out snapshot);
    }

    public void TransitionMatchPhase(MatchPhase nextPhase)
    {
        ThrowIfDisposed();
        simulation.TransitionMatchPhase(nextPhase);
    }

    public ForceStartResult ForceStart()
    {
        ThrowIfDisposed();
        return simulation.ForceStart();
    }

    public IReadOnlyList<ServerSnapshot> DrainSnapshots(InProcessClientConnection client)
    {
        InProcessClientState state = GetClientState(client);
        var snapshots = new List<ServerSnapshot>(state.Snapshots.Count);

        while (state.Snapshots.TryDequeue(out ServerSnapshot? snapshot))
            snapshots.Add(snapshot);

        return snapshots;
    }

    public void Step()
    {
        ThrowIfDisposed();

        var inputCommands = new Dictionary<ServerPlayerId, PlayerInputCommand>();

        foreach (InProcessClientState client in clients.Values)
        {
            if (client.InputCommands.TryDequeue(out PlayerInputCommand command))
                inputCommands[client.PlayerId] = command;
        }

        foreach ((ServerPlayerId playerId, BotInputState inputState) in
            botInputs.OrderBy(pair => pair.Key.Value))
        {
            if (inputState.PendingCommand is PlayerInputCommand command)
            {
                inputCommands[playerId] = command;
                inputState.PendingCommand = null;
            }
        }

        simulation.Step(inputCommands);

        foreach (InProcessClientState client in clients.Values)
            client.Snapshots.Enqueue(simulation.CreateSnapshot(client.PlayerId));
    }

    public void DisconnectClient(InProcessClientConnection client)
    {
        InProcessClientState state = GetClientState(client);

        clients.Remove(state.ConnectionId);
        simulation.RemovePlayer(state.PlayerId);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        clients.Clear();
        botInputs.Clear();
        simulation.Dispose();
        disposed = true;
    }

    private InProcessClientState GetClientState(InProcessClientConnection client)
    {
        ThrowIfDisposed();

        if (!clients.TryGetValue(client.ConnectionId, out InProcessClientState? state) ||
            state.PlayerId != client.PlayerId)
        {
            throw new InvalidOperationException(
                $"Unknown or disconnected in-process client connection '{client.ConnectionId}' for player '{client.PlayerId}'.");
        }

        return state;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private ServerPlayerDebugState CreatePlayerDebugState(
        AuthoritativePlayerState player,
        IReadOnlyDictionary<ServerPlayerId, int>? peerIdsByPlayerId,
        int queuedInputCount)
    {
        int? peerId = peerIdsByPlayerId is not null &&
            peerIdsByPlayerId.TryGetValue(player.PlayerId, out int mappedPeerId)
            ? mappedPeerId
            : null;

        return new ServerPlayerDebugState(
            CurrentTick,
            peerId,
            player.ConnectionId?.Value ?? 0,
            player.PlayerId.Value,
            player.Kind,
            player.Character.Position,
            player.Character.Velocity,
            player.Look.YawRadians,
            player.Look.PitchRadians,
            player.Health.CurrentHealth,
            player.Health.MaxHealth,
            player.Health.Alive,
            player.Weapon.WeaponId,
            player.Weapon.AmmoInMagazine,
            player.Weapon.ReserveAmmo,
            player.Weapon.IsReloading,
            player.LastProcessedInputSequence,
            player.LastProcessedInputClientTick,
            queuedInputCount);
    }

    private sealed class InProcessClientState(ServerConnectionId connectionId, ServerPlayerId playerId)
    {
        public ServerConnectionId ConnectionId { get; } = connectionId;

        public ServerPlayerId PlayerId { get; } = playerId;

        public Queue<PlayerInputCommand> InputCommands { get; } = [];

        public Queue<ServerSnapshot> Snapshots { get; } = [];
    }

    private sealed class BotInputState
    {
        public uint NextSequence { get; set; } = 1;

        public PlayerInputCommand? PendingCommand { get; set; }
    }
}

public readonly record struct InProcessClientConnection(
    ServerConnectionId ConnectionId,
    ServerPlayerId PlayerId);
