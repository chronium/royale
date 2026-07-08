using System.Numerics;
using Royale.Protocol;
using Royale.Server;
using WattleScript.Interpreter;

namespace Royale.Gameplay.Tests;

public sealed class ScenarioApi : ScenarioScriptObject, IDisposable
{
    internal const int MaxClockWaitTicks = 10000;

    private readonly Dictionary<uint, ScenarioPlayerApi> playersById = [];
    private readonly List<ScenarioEventApi> recordedEvents = [];
    private InProcessServerSession? session;

    public ScenarioApi()
    {
        server = new ScenarioServerApi(this);
        players = new ScenarioPlayersApi(this);
        observe = new ScenarioObservationsApi(this);
        assert = new ScenarioAssertApi(this);
        clock = new ScenarioClockApi(this);
        artifacts = new ScenarioArtifactsApi();
        events = new ScenarioEventsApi(this);
    }

    public ScenarioServerApi server { get; }

    public ScenarioPlayersApi players { get; }

    public ScenarioObservationsApi observe { get; }

    public ScenarioAssertApi assert { get; }

    public ScenarioClockApi clock { get; }

    public ScenarioArtifactsApi artifacts { get; }

    public ScenarioEventsApi events { get; }

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
        RecordEvent("server.started", detail: mapId);
    }

    internal void StopServer()
    {
        if (session is null)
            return;

        RecordEvent("server.stopped");

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

        StepRunningServer(count);
    }

    internal ulong WaitTicks(int count)
    {
        if (count < 1 || count > MaxClockWaitTicks)
            throw new ScriptRuntimeException(
                $"scenario.clock.waitTicks requires a tick count from 1 to {MaxClockWaitTicks}.");

        StepRunningServer(count);

        return CurrentTick;
    }

    internal bool WaitUntil(ScriptExecutionContext context, int maxTicks, DynValue predicate)
    {
        return WaitUntil(
            context,
            maxTicks,
            predicate,
            "scenario.clock.waitUntil",
            timeoutMessage: null,
            timeoutThrows: false,
            description: null);
    }

    internal void AssertEventually(
        ScriptExecutionContext context,
        int maxTicks,
        DynValue predicate,
        string description)
    {
        string checkedDescription = string.IsNullOrWhiteSpace(description)
            ? "condition"
            : description;

        WaitUntil(
            context,
            maxTicks,
            predicate,
            "scenario.assert.eventually",
            $"scenario.assert.eventually failed after {maxTicks} ticks: {checkedDescription}.",
            timeoutThrows: true,
            checkedDescription);
    }

    private void StepRunningServer(int count)
    {
        InProcessServerSession runningSession = RequireRunningSession();

        for (int i = 0; i < count; i++)
        {
            runningSession.Step();
            RecordEvent("server.stepped");
        }
    }

    private bool WaitUntil(
        ScriptExecutionContext context,
        int maxTicks,
        DynValue predicate,
        string apiName,
        string? timeoutMessage,
        bool timeoutThrows,
        string? description)
    {
        if (maxTicks < 0 || maxTicks > MaxClockWaitTicks)
            throw new ScriptRuntimeException($"{apiName} requires maxTicks from 0 to {MaxClockWaitTicks}.");

        RequirePredicate(predicate, apiName);
        RequireRunningSession();

        if (EvaluatePredicate(context, predicate, apiName))
        {
            RecordEvent("clock.wait.satisfied", detail: description);
            return true;
        }

        for (int i = 0; i < maxTicks; i++)
        {
            StepRunningServer(1);

            if (EvaluatePredicate(context, predicate, apiName))
            {
                RecordEvent("clock.wait.satisfied", detail: description);
                return true;
            }
        }

        RecordEvent("clock.wait.timeout", detail: description);

        if (timeoutThrows)
            throw new ScriptRuntimeException(timeoutMessage ?? $"{apiName} timed out after {maxTicks} ticks.");

        return false;
    }

    private static void RequirePredicate(DynValue predicate, string apiName)
    {
        if (predicate.Type is not (DataType.Function or DataType.ClrFunction))
            throw new ScriptRuntimeException($"{apiName} requires a predicate function.");
    }

    private static bool EvaluatePredicate(ScriptExecutionContext context, DynValue predicate, string apiName)
    {
        DynValue result = context.GetScript().Call(predicate);

        if (result.Type != DataType.Boolean)
        {
            throw new ScriptRuntimeException(
                $"{apiName} predicate must return a boolean, got {result.Type.ToLuaTypeString()}.");
        }

        return result.Boolean;
    }

    internal ScenarioPlayerApi ConnectPlayer()
    {
        InProcessServerSession runningSession = RequireRunningSession();
        InProcessClientConnection connection = runningSession.ConnectClient();
        var player = new ScenarioPlayerApi(connection);

        playersById.Add(player.playerId, player);
        RecordEvent("player.connected", player.playerId);

        return player;
    }

    internal void DisconnectPlayer(ScenarioPlayerApi player)
    {
        InProcessServerSession runningSession = RequireRunningSession();
        ScenarioPlayerApi connectedPlayer = RequireConnectedPlayer(player);

        runningSession.DisconnectClient(connectedPlayer.Connection);
        connectedPlayer.MarkDisconnected();
        playersById.Remove(connectedPlayer.playerId);
        RecordEvent("player.disconnected", connectedPlayer.playerId);
    }

    internal bool EnqueueInputCommand(ScenarioPlayerApi player, DynValue commandTable)
    {
        InProcessServerSession runningSession = RequireRunningSession();
        ScenarioPlayerApi connectedPlayer = RequireConnectedPlayer(player);
        PlayerInputCommand command = ParseInputCommand(commandTable);

        bool accepted = runningSession.TryEnqueueInputCommand(connectedPlayer.Connection, command);
        RecordEvent(
            accepted ? "player.input.accepted" : "player.input.rejected",
            connectedPlayer.playerId,
            $"sequence={command.Sequence}");

        return accepted;
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

    internal ServerPlayerDebugState GetPlayerDebugState(ScenarioPlayerApi player)
    {
        InProcessServerSession runningSession = RequireRunningSession();
        ScenarioPlayerApi connectedPlayer = RequireConnectedPlayer(player);

        foreach (ServerPlayerDebugState debugState in runningSession.GetPlayerDebugStates())
        {
            if (debugState.PlayerId == connectedPlayer.playerId)
                return debugState;
        }

        throw new ScriptRuntimeException(
            $"No debug state was available for scenario player '{connectedPlayer.playerId}'.");
    }

    public void Dispose()
    {
        StopServer();
    }

    internal IReadOnlyList<ScenarioEventApi> RecordedEvents => recordedEvents;

    internal ScenarioEventApi? LatestEvent => recordedEvents.Count == 0 ? null : recordedEvents[^1];

    internal bool HasEvent(string type) =>
        recordedEvents.Any(e => string.Equals(e.type, type, StringComparison.Ordinal));

    internal void ClearEvents() => recordedEvents.Clear();

    private void RecordEvent(string type, uint? playerId = null, string? detail = null)
    {
        recordedEvents.Add(new ScenarioEventApi(type, CurrentTick, playerId, detail));
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

    private static PlayerInputCommand ParseInputCommand(DynValue commandTable)
    {
        if (commandTable.Type != DataType.Table)
            throw new ScriptRuntimeException("scenario.players.input requires a command table.");

        Table table = commandTable.Table;
        InputButtons buttons = InputButtons.None;

        if (ReadOptionalBoolean(table, "jump"))
            buttons |= InputButtons.Jump;
        if (ReadOptionalBoolean(table, "fire"))
            buttons |= InputButtons.Fire;
        if (ReadOptionalBoolean(table, "reload"))
            buttons |= InputButtons.Reload;
        if (ReadOptionalBoolean(table, "interact"))
            buttons |= InputButtons.Interact;
        if (ReadOptionalBoolean(table, "crouch"))
            buttons |= InputButtons.Crouch;

        return new PlayerInputCommand(
            Sequence: ReadRequiredUInt32(table, "sequence"),
            ClientTick: ReadRequiredUInt32(table, "clientTick"),
            Move: new Vector2(
                ReadRequiredSingle(table, "moveX"),
                ReadRequiredSingle(table, "moveY")),
            YawRadians: ReadRequiredSingle(table, "yawRadians"),
            PitchRadians: ReadRequiredSingle(table, "pitchRadians"),
            Buttons: buttons);
    }

    private static uint ReadRequiredUInt32(Table table, string fieldName)
    {
        DynValue value = ReadRequired(table, fieldName);

        if (value.Type != DataType.Number)
            throw new ScriptRuntimeException($"scenario.players.input field '{fieldName}' must be a number.");

        double number = value.Number;
        if (!double.IsFinite(number) || number < uint.MinValue || number > uint.MaxValue || number % 1.0 != 0.0)
            throw new ScriptRuntimeException($"scenario.players.input field '{fieldName}' must be a uint32 integer.");

        return (uint)number;
    }

    private static float ReadRequiredSingle(Table table, string fieldName)
    {
        DynValue value = ReadRequired(table, fieldName);

        if (value.Type != DataType.Number)
            throw new ScriptRuntimeException($"scenario.players.input field '{fieldName}' must be a number.");

        return (float)value.Number;
    }

    private static bool ReadOptionalBoolean(Table table, string fieldName)
    {
        DynValue value = table.Get(fieldName);

        if (value.IsNil())
            return false;

        if (value.Type != DataType.Boolean)
            throw new ScriptRuntimeException($"scenario.players.input field '{fieldName}' must be a boolean.");

        return value.Boolean;
    }

    private static DynValue ReadRequired(Table table, string fieldName)
    {
        DynValue value = table.Get(fieldName);

        if (value.IsNil())
            throw new ScriptRuntimeException($"scenario.players.input requires field '{fieldName}'.");

        return value;
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

    public bool input(ScenarioPlayerApi player, DynValue command) => scenario.EnqueueInputCommand(player, command);
}

public sealed class ScenarioObservationsApi(ScenarioApi scenario) : ScenarioScriptObject
{
    public int connectedPlayerCount => scenario.ConnectedPlayerCount;

    public int livingPlayerCount => scenario.LivingPlayerCount;

    public ScenarioSnapshotApi latest(ScenarioPlayerApi player) => new(scenario.GetLatestSnapshot(player));

    public ScenarioPlayerDebugStateApi debugPlayer(ScenarioPlayerApi player) => new(scenario.GetPlayerDebugState(player));
}

public sealed class ScenarioAssertApi(ScenarioApi scenario) : ScenarioScriptObject
{
    public void equal(DynValue expected, DynValue actual)
    {
        if (!AreEqual(expected, actual))
        {
            throw new ScriptRuntimeException(
                $"scenario.assert.equal failed: expected {Format(expected)}, got {Format(actual)}.");
        }
    }

    public void near(DynValue expected, DynValue actual, DynValue tolerance)
    {
        double expectedNumber = ReadNumber(expected, "expected");
        double actualNumber = ReadNumber(actual, "actual");
        double toleranceNumber = ReadNumber(tolerance, "tolerance");

        if (!double.IsFinite(toleranceNumber) || toleranceNumber < 0.0)
            throw new ScriptRuntimeException("scenario.assert.near requires a finite non-negative tolerance.");

        double delta = Math.Abs(expectedNumber - actualNumber);

        if (delta > toleranceNumber)
        {
            throw new ScriptRuntimeException(
                "scenario.assert.near failed: " +
                $"expected {Format(expected)}, got {Format(actual)}, tolerance {Format(tolerance)}, delta " +
                delta.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ".");
        }
    }

    public void state(ScriptExecutionContext context, string name, DynValue predicate)
    {
        string checkedName = string.IsNullOrWhiteSpace(name) ? "state" : name;
        RequireAssertPredicate(predicate, "scenario.assert.state");

        DynValue result = context.GetScript().Call(predicate);
        if (result.Type != DataType.Boolean)
        {
            throw new ScriptRuntimeException(
                $"scenario.assert.state predicate must return a boolean, got {result.Type.ToLuaTypeString()}.");
        }

        if (!result.Boolean)
            throw new ScriptRuntimeException($"scenario.assert.state failed: {checkedName}.");
    }

    public void @event(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ScriptRuntimeException("scenario.assert.event requires a non-empty event type.");

        if (!scenario.HasEvent(type))
            throw new ScriptRuntimeException($"scenario.assert.event failed: missing event '{type}'.");
    }

    public void eventually(ScriptExecutionContext context, int maxTicks, DynValue predicate, string description) =>
        scenario.AssertEventually(context, maxTicks, predicate, description);

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

    private static double ReadNumber(DynValue value, string fieldName)
    {
        if (value.Type != DataType.Number)
            throw new ScriptRuntimeException($"scenario.assert.near {fieldName} must be a number.");

        if (!double.IsFinite(value.Number))
            throw new ScriptRuntimeException($"scenario.assert.near {fieldName} must be finite.");

        return value.Number;
    }

    private static void RequireAssertPredicate(DynValue predicate, string apiName)
    {
        if (predicate.Type is not (DataType.Function or DataType.ClrFunction))
            throw new ScriptRuntimeException($"{apiName} requires a predicate function.");
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

    public int maxWaitTicks => ScenarioApi.MaxClockWaitTicks;

    public ulong waitTicks(int count) => scenario.WaitTicks(count);

    public bool waitUntil(ScriptExecutionContext context, int maxTicks, DynValue predicate) =>
        scenario.WaitUntil(context, maxTicks, predicate);
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

public sealed class ScenarioEventsApi(ScenarioApi scenario) : ScenarioScriptObject
{
    public int count => scenario.RecordedEvents.Count;

    public string[] types => scenario.RecordedEvents.Select(e => e.type).ToArray();

    public ScenarioEventApi? latest => scenario.LatestEvent;

    public void clear() => scenario.ClearEvents();
}

public sealed class ScenarioEventApi(string type, ulong tick, uint? playerId, string? detail) : ScenarioScriptObject
{
    public string type { get; } = type;

    public ulong tick { get; } = tick;

    public uint? playerId { get; } = playerId;

    public string? detail { get; } = detail;
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

    public ScenarioPlayerSnapshotApi[] players => snapshot.Players
        .OrderBy(p => p.PlayerId)
        .Select(p => new ScenarioPlayerSnapshotApi(p))
        .ToArray();

    public ScenarioMatchSnapshotApi match => new(snapshot.Match);

    public ScenarioSafeZoneSnapshotApi safeZone => new(snapshot.SafeZone);

    public ScenarioPlayerSnapshotApi? player(uint playerId)
    {
        foreach (PlayerSnapshotState player in snapshot.Players.OrderBy(p => p.PlayerId))
        {
            if (player.PlayerId == playerId)
                return new ScenarioPlayerSnapshotApi(player);
        }

        return null;
    }
}

public sealed class ScenarioPlayerSnapshotApi(PlayerSnapshotState player) : ScenarioScriptObject
{
    public uint playerId => player.PlayerId;

    public ScenarioVector3Api position => new(player.Position);

    public ScenarioVector3Api velocity => new(player.Velocity);

    public ScenarioLookSnapshotApi look => new(player.YawRadians, player.PitchRadians);

    public int currentHealth => player.CurrentHealth;

    public int maxHealth => player.MaxHealth;

    public ScenarioHealthSnapshotApi health => new(player.CurrentHealth, player.MaxHealth);

    public bool alive => player.Alive;

    public ScenarioWeaponSnapshotApi weapon => new(player.Weapon);
}

public sealed class ScenarioPlayerDebugStateApi(ServerPlayerDebugState player) : ScenarioScriptObject
{
    public ulong serverTick => player.ServerTick;

    public int? peerId => player.PeerId;

    public uint connectionId => player.ConnectionId;

    public uint playerId => player.PlayerId;

    public ScenarioVector3Api position => new(player.Position);

    public ScenarioVector3Api velocity => new(player.Velocity);

    public ScenarioLookSnapshotApi look => new(player.YawRadians, player.PitchRadians);

    public int currentHealth => player.CurrentHealth;

    public int maxHealth => player.MaxHealth;

    public ScenarioHealthSnapshotApi health => new(player.CurrentHealth, player.MaxHealth);

    public bool alive => player.Alive;

    public ScenarioPlayerDebugWeaponApi weapon => new(player);

    public uint? lastProcessedInputSequence => player.LastProcessedInputSequence;

    public int queuedInputCount => player.QueuedInputCount;
}

public sealed class ScenarioPlayerDebugWeaponApi(ServerPlayerDebugState player) : ScenarioScriptObject
{
    public string weaponId => player.WeaponId;

    public int ammoInMagazine => player.AmmoInMagazine;

    public int reserveAmmo => player.ReserveAmmo;

    public bool isReloading => player.IsReloading;
}

public sealed class ScenarioVector3Api(Vector3 vector) : ScenarioScriptObject
{
    public float x => vector.X;

    public float y => vector.Y;

    public float z => vector.Z;
}

public sealed class ScenarioLookSnapshotApi(float yawRadians, float pitchRadians) : ScenarioScriptObject
{
    public float yawRadians { get; } = yawRadians;

    public float pitchRadians { get; } = pitchRadians;
}

public sealed class ScenarioHealthSnapshotApi(int current, int max) : ScenarioScriptObject
{
    public int current { get; } = current;

    public int max { get; } = max;
}

public sealed class ScenarioWeaponSnapshotApi(WeaponSnapshotState weapon) : ScenarioScriptObject
{
    public string weaponId => weapon.WeaponId;

    public int ammoInMagazine => weapon.AmmoInMagazine;

    public int reserveAmmo => weapon.ReserveAmmo;

    public ulong nextAllowedFireTick => weapon.NextAllowedFireTick;

    public ulong? lastFiredTick => weapon.LastFiredTick;

    public bool isReloading => weapon.IsReloading;

    public ulong? reloadCompleteTick => weapon.ReloadCompleteTick;
}

public sealed class ScenarioMatchSnapshotApi(MatchSnapshotState match) : ScenarioScriptObject
{
    public string phase => match.Phase.ToString();

    public ulong phaseStartedTick => match.PhaseStartedTick;

    public int livingPlayerCount => match.LivingPlayerCount;

    public uint? winnerPlayerId => match.WinnerPlayerId;
}

public sealed class ScenarioSafeZoneSnapshotApi(SafeZoneSnapshotState safeZone) : ScenarioScriptObject
{
    public ScenarioVector3Api center => new(safeZone.Center);

    public float currentRadius => safeZone.CurrentRadius;

    public float targetRadius => safeZone.TargetRadius;

    public ulong lastUpdatedTick => safeZone.LastUpdatedTick;
}

[WattleScriptHideMember("__new")]
[WattleScriptHideMember("Dispose")]
[WattleScriptHideMember("Equals")]
[WattleScriptHideMember("GetHashCode")]
[WattleScriptHideMember("GetType")]
[WattleScriptHideMember("ToString")]
public abstract class ScenarioScriptObject;
