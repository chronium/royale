using System.Numerics;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Server.Bots;
using Royale.Server.Match;
using Royale.Server.Simulation;

namespace Royale.Server.Sessions;

public sealed class InProcessServerSession : IDisposable
{
    private readonly HeadlessServerSimulation simulation;
    private readonly Dictionary<ServerConnectionId, InProcessClientState> clients = [];
    private readonly Dictionary<ServerPlayerId, BotInputState> botInputs = [];
    private readonly BotNavigationSystem botNavigation;
    private uint nextConnectionId = 1;
    private bool disposed;

    private InProcessServerSession(HeadlessServerSimulation simulation)
    {
        this.simulation = simulation;
        botNavigation = new BotNavigationSystem(simulation.MapId, simulation.WorldBounds, simulation.NavigationGraph);
    }

    public ulong CurrentTick => simulation.CurrentTick;

    public string MapId => simulation.MapId;

    public int ActivePlayerCount => simulation.ActivePlayerCount;

    public int HumanPlayerCount => simulation.HumanPlayerCount;

    public int BotPlayerCount => simulation.BotPlayerCount;

    public int ConnectedClientCount => clients.Count;

    public int LivingPlayerCount => simulation.LivingPlayerCount;

    public MatchPhase MatchPhase => simulation.MatchState.Phase;

    public bool GeneratesAutonomousBotInput =>
        MatchPhase == MatchPhase.Playing &&
        simulation.Players.Values.Any(player => player.Kind == ServerPlayerKind.Bot && player.Health.Alive);

    public MatchStartSettings MatchStartSettings => simulation.MatchStartSettings;

    public MatchStartReason? LastMatchStartReason => simulation.LastMatchStartReason;

    public int QueuedInputCommandCount =>
        clients.Values.Sum(client => client.InputCommands.Count) +
        botInputs.Values.Sum(input => input.Commands.Count);

    public bool IsDisposed => disposed;

    public bool IsSimulationDisposed => simulation.IsDisposed;

    public static InProcessServerSession Create(
        string mapId,
        MatchStartSettings? matchStartSettings = null,
        int? spawnSeed = null) =>
        new(HeadlessServerSimulation.Create(mapId, matchStartSettings, spawnSeed));

    public static InProcessServerSession Create(
        GameMap map,
        MatchStartSettings? matchStartSettings = null,
        int? spawnSeed = null,
        DirectoryInfo? assetRoot = null) =>
        new(HeadlessServerSimulation.Create(map, matchStartSettings, spawnSeed, assetRoot));

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
                    : botInputs.TryGetValue(player.PlayerId, out BotInputState? botInput)
                        ? botInput.Commands.Count
                        : 0))
            .ToArray();
    }

    public InProcessClientConnection ConnectClient()
    {
        ThrowIfDisposed();

        if (TryConnectClient(out InProcessClientConnection connection, out ClientAdmissionFailure? failure))
            return connection;

        throw new InvalidOperationException(failure?.Detail ?? "The match is not accepting new clients.");
    }

    public bool TryConnectClient(
        out InProcessClientConnection connection,
        out ClientAdmissionFailure? failure)
    {
        ThrowIfDisposed();

        connection = default;
        ServerConnectionId connectionId = PeekNextConnectionId();
        AuthoritativePlayerState player;

        if (MatchPhase == MatchPhase.WaitingForPlayers)
        {
            if (ActivePlayerCount >= MatchStartSettings.TargetPlayers)
            {
                failure = ClientAdmissionFailure.RosterFull;
                return false;
            }

            player = simulation.AddHumanPlayer(connectionId);
        }
        else if (MatchPhase == MatchPhase.Countdown)
        {
            AuthoritativePlayerState? bot = simulation.Players.Values
                .Where(candidate => candidate.Kind == ServerPlayerKind.Bot)
                .OrderBy(candidate => candidate.PlayerId.Value)
                .FirstOrDefault();
            if (bot is null)
            {
                failure = ClientAdmissionFailure.RosterFull;
                return false;
            }

            if (!simulation.TryConvertBotToHuman(bot.PlayerId, connectionId))
                throw new InvalidOperationException($"Bot participant '{bot.PlayerId}' could not be assigned to a client.");

            botInputs.Remove(bot.PlayerId);
            botNavigation.Remove(bot.PlayerId);
            player = simulation.Players[bot.PlayerId];
        }
        else
        {
            failure = ClientAdmissionFailure.RosterLocked;
            return false;
        }

        CommitConnectionId(connectionId);
        connection = new InProcessClientConnection(connectionId, player.PlayerId);
        var state = new InProcessClientState(connectionId, player.PlayerId);
        state.Snapshots.Enqueue(simulation.CreateSnapshot(player.PlayerId));
        clients.Add(connectionId, state);
        failure = null;
        return true;
    }

    public ServerPlayerId AddBot()
    {
        ThrowIfDisposed();
        ServerPlayerId playerId = simulation.AddBotPlayer().PlayerId;
        botInputs.Add(playerId, new BotInputState());
        botNavigation.Add(playerId);
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
        {
            botInputs.Remove(playerId);
            botNavigation.Remove(playerId);
        }

        return removed;
    }

    public bool TryAssignBotNavigationGoal(ServerPlayerId playerId, Vector3 goal)
    {
        ThrowIfDisposed();
        return simulation.TryGetPlayer(playerId, out AuthoritativePlayerState? player) &&
            player?.Kind == ServerPlayerKind.Bot &&
            botNavigation.TryAssignGoal(playerId, goal);
    }

    public bool TryClearBotNavigationGoal(ServerPlayerId playerId)
    {
        ThrowIfDisposed();
        return simulation.TryGetPlayer(playerId, out AuthoritativePlayerState? player) &&
            player?.Kind == ServerPlayerKind.Bot &&
            botNavigation.TryClearGoal(playerId);
    }

    public bool TrySubmitBotInput(
        ServerPlayerId playerId,
        BotInputIntent intent,
        int delayTicks = 0)
    {
        ThrowIfDisposed();

        if (delayTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks, "Bot input delay must be non-negative.");

        if (!simulation.TryGetPlayer(playerId, out AuthoritativePlayerState? player) ||
            player is null ||
            player.Kind != ServerPlayerKind.Bot ||
            !botInputs.TryGetValue(playerId, out BotInputState? inputState) ||
            inputState.LastGenerationTick == CurrentTick)
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

        PlayerInputCommand command = validationCommand with
        {
            Sequence = inputState.NextSequence,
            ClientTick = CurrentTick >= uint.MaxValue ? uint.MaxValue : (uint)CurrentTick,
        };
        ulong nominalScheduledTick = (ulong)delayTicks > ulong.MaxValue - CurrentTick
            ? ulong.MaxValue
            : CurrentTick + (ulong)delayTicks;
        ulong scheduledTick = inputState.Commands.Count > 0
            ? Math.Max(nominalScheduledTick, inputState.LastScheduledTick)
            : nominalScheduledTick;
        inputState.Commands.Enqueue(new ScheduledBotInput(command, scheduledTick));
        inputState.LastGenerationTick = CurrentTick;
        inputState.LastScheduledTick = scheduledTick;
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
        ForceStartResult result = simulation.ForceStart();
        SynchronizeBotInputs();
        return result;
    }

    public IReadOnlyList<ServerSnapshot> DrainSnapshots(InProcessClientConnection client)
    {
        InProcessClientState state = GetClientState(client);
        var snapshots = new List<ServerSnapshot>(state.Snapshots.Count);

        while (state.Snapshots.TryDequeue(out ServerSnapshot? snapshot))
            snapshots.Add(snapshot);

        return snapshots;
    }

    public void Step(int autonomousBotDelayTicks = 0)
    {
        ThrowIfDisposed();

        if (autonomousBotDelayTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(autonomousBotDelayTicks));

        GenerateAutonomousBotInputs(autonomousBotDelayTicks);

        var inputCommands = new Dictionary<ServerPlayerId, PlayerInputCommand>();

        foreach (InProcessClientState client in clients.Values)
        {
            if (client.InputCommands.TryDequeue(out PlayerInputCommand command))
                inputCommands[client.PlayerId] = command;
        }

        foreach ((ServerPlayerId playerId, BotInputState inputState) in
            botInputs.OrderBy(pair => pair.Key.Value))
        {
            if (inputState.Commands.TryPeek(out ScheduledBotInput scheduled) &&
                scheduled.ScheduledTick <= CurrentTick)
            {
                inputCommands[playerId] = scheduled.Command;
                inputState.Commands.Dequeue();
            }
        }

        simulation.Step(inputCommands);
        SynchronizeBotInputs();

        foreach (InProcessClientState client in clients.Values)
            client.Snapshots.Enqueue(simulation.CreateSnapshot(client.PlayerId));
    }

    public void DisconnectClient(InProcessClientConnection client)
    {
        InProcessClientState state = GetClientState(client);

        clients.Remove(state.ConnectionId);

        if (simulation.TryConvertHumanToBot(state.PlayerId))
        {
            botInputs[state.PlayerId] = new BotInputState();
            botNavigation.Add(state.PlayerId);
        }
        else
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

    private void SynchronizeBotInputs()
    {
        foreach (ServerPlayerId playerId in botInputs.Keys.ToArray())
        {
            if (!simulation.TryGetPlayer(playerId, out AuthoritativePlayerState? player) ||
                player?.Kind != ServerPlayerKind.Bot)
            {
                botInputs.Remove(playerId);
                botNavigation.Remove(playerId);
            }
        }

        foreach (AuthoritativePlayerState bot in simulation.Players.Values
                     .Where(player => player.Kind == ServerPlayerKind.Bot))
        {
            if (botInputs.TryAdd(bot.PlayerId, new BotInputState()))
                botNavigation.Add(bot.PlayerId);
        }
    }

    private void GenerateAutonomousBotInputs(int delayTicks)
    {
        foreach (AuthoritativePlayerState bot in simulation.Players.Values
                     .Where(player => player.Kind == ServerPlayerKind.Bot)
                     .OrderBy(player => player.PlayerId.Value))
        {
            if (botNavigation.TryGenerate(bot, MatchPhase, CurrentTick, out BotInputIntent intent))
                TrySubmitBotInput(bot.PlayerId, intent, delayTicks);
        }
    }

    private ServerConnectionId PeekNextConnectionId()
        => new(nextConnectionId == 0 ? 1 : nextConnectionId);

    private void CommitConnectionId(ServerConnectionId connectionId) =>
        nextConnectionId = unchecked(connectionId.Value + 1);

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
            queuedInputCount,
            player.Character.Stance,
            simulation.CharacterSettings.GetHeight(player.Character.Stance),
            player.Character.IsSprinting);
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

        public ulong? LastGenerationTick { get; set; }

        public ulong LastScheduledTick { get; set; }

        public Queue<ScheduledBotInput> Commands { get; } = [];
    }

    private readonly record struct ScheduledBotInput(
        PlayerInputCommand Command,
        ulong ScheduledTick);
}

public readonly record struct InProcessClientConnection(
    ServerConnectionId ConnectionId,
    ServerPlayerId PlayerId);

public sealed record ClientAdmissionFailure(ServerRejectReason Reason, string Detail)
{
    public static ClientAdmissionFailure RosterFull { get; } = new(
        ServerRejectReason.MatchUnavailable,
        "The match roster is already full.");

    public static ClientAdmissionFailure RosterLocked { get; } = new(
        ServerRejectReason.MatchUnavailable,
        "The match roster is locked after preparation.");
}
