using Royale.Protocol;
using Royale.Content;

namespace Royale.Server;

public sealed class InProcessServerSession : IDisposable
{
    private readonly HeadlessServerSimulation simulation;
    private readonly Dictionary<ServerConnectionId, InProcessClientState> clients = [];
    private uint nextConnectionId = 1;
    private bool disposed;

    private InProcessServerSession(HeadlessServerSimulation simulation)
    {
        this.simulation = simulation;
    }

    public ulong CurrentTick => simulation.CurrentTick;

    public string MapId => simulation.MapId;

    public int ActivePlayerCount => simulation.Players.Count;

    public int ConnectedClientCount => clients.Count;

    public bool IsDisposed => disposed;

    public bool IsSimulationDisposed => simulation.IsDisposed;

    public static InProcessServerSession Create(string mapId) =>
        new(HeadlessServerSimulation.Create(mapId));

    public static InProcessServerSession Create(GameMap map) =>
        new(HeadlessServerSimulation.Create(map));

    public InProcessClientConnection ConnectClient()
    {
        ThrowIfDisposed();

        ServerConnectionId connectionId = new(nextConnectionId++);
        AuthoritativePlayerState player = simulation.AddPlayer(connectionId);
        var client = new InProcessClientConnection(connectionId, player.PlayerId);
        var state = new InProcessClientState(connectionId, player.PlayerId);

        state.Snapshots.Enqueue(simulation.CreateSnapshot(player.PlayerId));
        clients.Add(connectionId, state);

        return client;
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
            while (client.InputCommands.TryDequeue(out PlayerInputCommand command))
                inputCommands[client.PlayerId] = command;
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

    private sealed class InProcessClientState(ServerConnectionId connectionId, ServerPlayerId playerId)
    {
        public ServerConnectionId ConnectionId { get; } = connectionId;

        public ServerPlayerId PlayerId { get; } = playerId;

        public Queue<PlayerInputCommand> InputCommands { get; } = [];

        public Queue<ServerSnapshot> Snapshots { get; } = [];
    }
}

public readonly record struct InProcessClientConnection(
    ServerConnectionId ConnectionId,
    ServerPlayerId PlayerId);
