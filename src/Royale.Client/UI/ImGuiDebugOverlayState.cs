using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Debug;
using Royale.Rendering.Meshes;
using Royale.Rendering.Screenshots;
using Royale.Rendering.Text;
using Royale.Client.Gameplay;
using Royale.Client.Networking;
using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

namespace Royale.Client.UI;

public readonly record struct ImGuiDebugOverlayState(
    double DeltaSeconds,
    int FixedTicksThisFrame,
    ulong TotalFixedTicks)
{
    public TelemetryRendererState? Renderer { get; init; }

    public TelemetrySimulationState Simulation { get; init; }

    public TelemetryPlayerState? Player { get; init; }

    public TelemetryPhysicsState? Physics { get; init; }

    public TelemetryServerState? Server { get; init; }

    public TelemetryNetworkState? Network { get; init; }

    public TelemetryConnectionState? Connection { get; init; }

    public double FramesPerSecond => DeltaSeconds > 0 ? 1.0 / DeltaSeconds : 0.0;

    public string FrameTimingText => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"Frame {DeltaSeconds * 1000.0:0.00} ms ({FramesPerSecond:0} FPS)");

    public string FixedTicksText => $"Fixed ticks this frame: {FixedTicksThisFrame}";

    public string TotalFixedTickText => $"Total fixed tick: {TotalFixedTicks}";

    public static ImGuiDebugOverlayState CreateOffline(
        double deltaSeconds,
        int fixedTicksThisFrame,
        ulong totalFixedTicks,
        TelemetryRendererState? renderer,
        LocalPlayerController localPlayer,
        int staticColliderCount)
    {
        ArgumentNullException.ThrowIfNull(localPlayer);

        return new ImGuiDebugOverlayState(
            deltaSeconds,
            fixedTicksThisFrame,
            totalFixedTicks)
        {
            Renderer = renderer,
            Simulation = new TelemetrySimulationState(null, null, null, null, null, null),
            Player = new TelemetryPlayerState(
                "Offline local player",
                new TelemetryPlayerValues(
                    "offline simulation",
                    localPlayer.CharacterState.Position,
                    localPlayer.CharacterState.Velocity,
                    localPlayer.LookState.YawRadians,
                    localPlayer.LookState.PitchRadians,
                    localPlayer.Health.CurrentHealth,
                    localPlayer.Health.MaxHealth,
                    localPlayer.Alive,
                    localPlayer.IsGrounded,
                    "offline simulation",
                    localPlayer.Weapon.Id,
                    null,
                    null,
                    localPlayer.Weapon.MagazineSize,
                    localPlayer.CharacterState.Stance.ToString(),
                    localPlayer.CharacterSettings.GetHeight(localPlayer.CharacterState.Stance),
                    localPlayer.CharacterState.IsSprinting),
                PlayerDiagnosticsState.FromPlayer(localPlayer)),
            Physics = new TelemetryPhysicsState(
                "offline collision world",
                true,
                staticColliderCount,
                null,
                null),
            Connection = new TelemetryConnectionState(
                "offline",
                null,
                null,
                "No network connection (offline mode)",
                null,
                null,
                null,
                null,
                null),
        };
    }

    public static ImGuiDebugOverlayState CreateNetworked(
        double deltaSeconds,
        int fixedTicksThisFrame,
        ulong totalFixedTicks,
        TelemetryRendererState? renderer,
        NetworkClientRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        ServerSnapshot? snapshot = runtime.State.LatestSnapshot;
        TelemetryPlayerState player = CreateNetworkPlayer(runtime, snapshot);
        TelemetryConnectionState connection = CreateConnection(runtime);

        return new ImGuiDebugOverlayState(
            deltaSeconds,
            fixedTicksThisFrame,
            totalFixedTicks)
        {
            Renderer = renderer,
            Simulation = new TelemetrySimulationState(
                snapshot?.ServerTick,
                snapshot is null ? null : CalculateTickDifference(snapshot.ServerTick, totalFixedTicks),
                runtime.PendingInputCount,
                runtime.LastReplayedInputCount,
                runtime.ReconciliationCount,
                runtime.LastPredictionCorrectionDistance),
            Player = player,
            Physics = new TelemetryPhysicsState(
                "client prediction collision world",
                runtime.Accepted ? runtime.PredictionMapAvailable : null,
                runtime.PredictionStaticColliderCount,
                runtime.Accepted ? runtime.PredictionActive : null,
                runtime.Accepted ? runtime.PredictionSeeded : null),
            Server = new TelemetryServerState(
                snapshot is not null
                    ? "Latest authoritative snapshot"
                    : runtime.Accepted
                        ? "Waiting for first authoritative snapshot"
                        : "Waiting for connection acceptance before snapshots",
                snapshot),
            Network = new TelemetryNetworkState(
                ClientNetworkTelemetryValues.FromDiagnostics(runtime.Diagnostics),
                runtime.LastTransportStatistics,
                runtime.RemoteSnapshotBufferCount,
                runtime.RemoteInterpolationDelayTicks,
                runtime.LastRemoteInterpolationTargetTick,
                runtime.LastRemoteRenderUsedInterpolation),
            Connection = connection,
        };
    }

    private static TelemetryPlayerState CreateNetworkPlayer(NetworkClientRuntime runtime, ServerSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new TelemetryPlayerState(
                runtime.Accepted
                    ? "Waiting for first authoritative player snapshot"
                    : "Waiting for connection acceptance",
                null,
                null);
        }

        if (!runtime.State.TryGetLocalPlayer(out PlayerSnapshotState player))
            return new TelemetryPlayerState("Latest snapshot does not contain the local player", null, null);

        return new TelemetryPlayerState(
            "Authoritative local player",
            new TelemetryPlayerValues(
                "authoritative snapshot",
                player.Position,
                player.Velocity,
                player.YawRadians,
                player.PitchRadians,
                player.CurrentHealth,
                player.MaxHealth,
                player.Alive,
                runtime.PredictionIsGrounded,
                runtime.PredictionIsGrounded.HasValue ? "prediction" : null,
                player.Weapon.WeaponId,
                player.Weapon.AmmoInMagazine,
                player.Weapon.ReserveAmmo,
                null,
                player.Crouched ? "Crouched" : "Standing",
                player.Crouched ? 1.1f : 1.8f,
                player.Sprinting),
            null);
    }

    private static TelemetryConnectionState CreateConnection(NetworkClientRuntime runtime)
    {
        string status = runtime.HandshakeState switch
        {
            NetworkHandshakeClientState.Pending => "Handshake pending",
            NetworkHandshakeClientState.Accepted => "Connection accepted",
            NetworkHandshakeClientState.Rejected => "Handshake rejected",
            NetworkHandshakeClientState.Disconnected => "Disconnected",
            null when runtime.HandshakeStarted => "Handshake starting",
            null => "Transport connecting",
            _ => "Unknown connection state",
        };

        ClientNetworkError? error = runtime.Diagnostics.LastNetworkError;
        return new TelemetryConnectionState(
            "connect",
            runtime.ServerEndpoint,
            runtime.ServerPeerId,
            status,
            runtime.HandshakeState,
            runtime.AcceptedSession,
            runtime.HandshakeRejection,
            runtime.Diagnostics.LastDisconnectReason,
            error is ClientNetworkError value
                ? new ClientNetworkErrorValues(value.Endpoint, value.SocketError)
                : null);
    }

    private static long CalculateTickDifference(ulong serverTick, ulong clientTick)
    {
        if (serverTick >= clientTick)
            return (long)Math.Min(serverTick - clientTick, (ulong)long.MaxValue);

        ulong difference = clientTick - serverTick;
        return difference > (ulong)long.MaxValue ? long.MinValue : -(long)difference;
    }
}
