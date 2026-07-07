using Royale.Protocol;
using Royale.Server;
using WattleScript.Interpreter;

namespace Royale.Gameplay.Tests;

public sealed class ScenarioApi : ScenarioScriptObject, IDisposable
{
    private readonly Dictionary<uint, ScenarioPlayerApi> playersById = [];
    private InProcessServerSession? session;

    public ScenarioApi()
    {
        server = new ScenarioServerApi(this);
        players = new ScenarioPlayersApi(this);
        observe = new ScenarioObservationsApi(this);
        assert = new ScenarioAssertApi();
        clock = new ScenarioClockApi(this);
        artifacts = new ScenarioArtifactsApi();
    }

    public ScenarioServerApi server { get; }

    public ScenarioPlayersApi players { get; }

    public ScenarioObservationsApi observe { get; }

    public ScenarioAssertApi assert { get; }

    public ScenarioClockApi clock { get; }

    public ScenarioArtifactsApi artifacts { get; }

    internal bool IsRunning => session is { IsDisposed: false };

    internal ulong CurrentTick => session?.CurrentTick ?? 0UL;

    internal int ConnectedPlayerCount => session?.ConnectedClientCount ?? 0;

    internal int LivingPlayerCount => session?.ActivePlayerCount ?? 0;

    internal void StartServer(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
            throw new ScriptRuntimeException("scenario.server.start requires a non-empty map id.");

        if (IsRunning)
            throw new ScriptRuntimeException("scenario server is already running.");

        session = InProcessServerSession.Create(mapId);
    }

    internal void StopServer()
    {
        if (session is null)
            return;

        foreach (ScenarioPlayerApi player in playersById.Values)
            player.MarkDisconnected();

        playersById.Clear();
        session.Dispose();
        session = null;
    }

    internal void StepServer(int count)
    {
        if (count < 1)
            throw new ScriptRuntimeException("scenario.server.step requires a positive tick count.");

        InProcessServerSession runningSession = RequireRunningSession();

        for (int i = 0; i < count; i++)
            runningSession.Step();
    }

    internal ScenarioPlayerApi ConnectPlayer()
    {
        InProcessServerSession runningSession = RequireRunningSession();
        InProcessClientConnection connection = runningSession.ConnectClient();
        var player = new ScenarioPlayerApi(connection);

        playersById.Add(player.playerId, player);

        return player;
    }

    internal void DisconnectPlayer(ScenarioPlayerApi player)
    {
        InProcessServerSession runningSession = RequireRunningSession();
        ScenarioPlayerApi connectedPlayer = RequireConnectedPlayer(player);

        runningSession.DisconnectClient(connectedPlayer.Connection);
        connectedPlayer.MarkDisconnected();
        playersById.Remove(connectedPlayer.playerId);
    }

    internal ServerSnapshot GetLatestSnapshot(ScenarioPlayerApi player)
    {
        InProcessServerSession runningSession = RequireRunningSession();
        ScenarioPlayerApi connectedPlayer = RequireConnectedPlayer(player);
        IReadOnlyList<ServerSnapshot> snapshots = runningSession.DrainSnapshots(connectedPlayer.Connection);

        if (snapshots.Count > 0)
            connectedPlayer.UpdateLatestSnapshot(snapshots[^1]);

        return connectedPlayer.LatestSnapshot
            ?? throw new ScriptRuntimeException(
                $"No snapshot has been produced for scenario player '{connectedPlayer.playerId}'.");
    }

    public void Dispose()
    {
        StopServer();
    }

    private InProcessServerSession RequireRunningSession() =>
        session is { IsDisposed: false } runningSession
            ? runningSession
            : throw new ScriptRuntimeException("scenario server is not running.");

    private ScenarioPlayerApi RequireConnectedPlayer(ScenarioPlayerApi? player)
    {
        if (player is null)
            throw new ScriptRuntimeException("scenario player handle is required.");

        if (!player.isConnected || !playersById.TryGetValue(player.playerId, out ScenarioPlayerApi? connectedPlayer))
            throw new ScriptRuntimeException($"scenario player '{player.playerId}' is not connected.");

        return connectedPlayer;
    }
}

public sealed class ScenarioServerApi(ScenarioApi scenario) : ScenarioScriptObject
{
    public bool isRunning => scenario.IsRunning;

    public ulong tick => scenario.CurrentTick;

    public void start(string mapId) => scenario.StartServer(mapId);

    public void stop() => scenario.StopServer();

    public void step(int count) => scenario.StepServer(count);
}

public sealed class ScenarioPlayersApi(ScenarioApi scenario) : ScenarioScriptObject
{
    public int count => scenario.ConnectedPlayerCount;

    public ScenarioPlayerApi connect() => scenario.ConnectPlayer();

    public void disconnect(ScenarioPlayerApi player) => scenario.DisconnectPlayer(player);
}

public sealed class ScenarioObservationsApi(ScenarioApi scenario) : ScenarioScriptObject
{
    public int connectedPlayerCount => scenario.ConnectedPlayerCount;

    public int livingPlayerCount => scenario.LivingPlayerCount;

    public ScenarioSnapshotApi latest(ScenarioPlayerApi player) => new(scenario.GetLatestSnapshot(player));
}

public sealed class ScenarioAssertApi : ScenarioScriptObject
{
    public void equal(DynValue expected, DynValue actual)
    {
        if (!AreEqual(expected, actual))
        {
            throw new ScriptRuntimeException(
                $"scenario.assert.equal failed: expected {Format(expected)}, got {Format(actual)}.");
        }
    }

    public void @true(DynValue value)
    {
        if (value.Type != DataType.Boolean || !value.Boolean)
            throw new ScriptRuntimeException($"scenario.assert.true failed: got {Format(value)}.");
    }

    public void isTrue(DynValue value) => @true(value);

    private static bool AreEqual(DynValue expected, DynValue actual)
    {
        if (expected.Type == DataType.Number && actual.Type == DataType.Number)
            return expected.Number.Equals(actual.Number);

        if (expected.Type != actual.Type)
            return false;

        return expected.Type switch
        {
            DataType.Nil or DataType.Void => true,
            DataType.Boolean => expected.Boolean == actual.Boolean,
            DataType.String => expected.String == actual.String,
            DataType.Number => expected.Number.Equals(actual.Number),
            _ => ReferenceEquals(expected.UserData?.Object, actual.UserData?.Object),
        };
    }

    private static string Format(DynValue value) =>
        value.Type switch
        {
            DataType.Nil => "nil",
            DataType.Void => "void",
            DataType.Boolean => value.Boolean ? "true" : "false",
            DataType.String => $"\"{value.String}\"",
            DataType.Number => value.Number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToPrintString(),
        };
}

public sealed class ScenarioClockApi(ScenarioApi scenario) : ScenarioScriptObject
{
    public ulong tick => scenario.CurrentTick;
}

public sealed class ScenarioArtifactsApi : ScenarioScriptObject
{
    private readonly Dictionary<string, string> records = [];

    public int count => records.Count;

    public string[] names => records.Keys.Order(StringComparer.Ordinal).ToArray();

    internal IReadOnlyDictionary<string, string> Records => records;

    public void record(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ScriptRuntimeException("scenario.artifacts.record requires a non-empty artifact name.");

        records[name] = value ?? string.Empty;
    }
}

public sealed class ScenarioPlayerApi : ScenarioScriptObject
{
    internal ScenarioPlayerApi(InProcessClientConnection connection)
    {
        Connection = connection;
    }

    public uint playerId => Connection.PlayerId.Value;

    public uint connectionId => Connection.ConnectionId.Value;

    public bool isConnected { get; private set; } = true;

    internal InProcessClientConnection Connection { get; }

    internal ServerSnapshot? LatestSnapshot { get; private set; }

    internal void UpdateLatestSnapshot(ServerSnapshot snapshot)
    {
        LatestSnapshot = snapshot;
    }

    internal void MarkDisconnected()
    {
        isConnected = false;
    }
}

public sealed class ScenarioSnapshotApi(ServerSnapshot snapshot) : ScenarioScriptObject
{
    public ulong serverTick => snapshot.ServerTick;

    public uint? localPlayerId => snapshot.LocalPlayerId;

    public uint? acknowledgedInputSequence => snapshot.AcknowledgedInputSequence;

    public int connectedPlayerCount => snapshot.Players.Count;

    public int livingPlayerCount => snapshot.Match.LivingPlayerCount;
}

[WattleScriptHideMember("__new")]
[WattleScriptHideMember("Dispose")]
[WattleScriptHideMember("Equals")]
[WattleScriptHideMember("GetHashCode")]
[WattleScriptHideMember("GetType")]
[WattleScriptHideMember("ToString")]
public abstract class ScenarioScriptObject;
