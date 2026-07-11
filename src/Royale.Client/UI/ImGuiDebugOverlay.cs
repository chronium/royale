using Evergine.Bindings.Imgui;
using Evergine.Mathematics;
using Royale.Client.Gameplay;
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

internal static unsafe class ImGuiDebugOverlay
{
    public static void Build(
        ImGuiDebugOverlayState state,
        LocalPlayerController? localPlayer = null,
        Action? debugKillPlayer = null,
        Action? debugRespawnPlayer = null)
    {
        BuildTelemetryWindow(state, localPlayer, debugKillPlayer, debugRespawnPlayer);

        if (localPlayer is not null)
            BuildTrainingDummyDiagnostics(localPlayer.TrainingDummy);
    }

    private static void BuildTelemetryWindow(
        ImGuiDebugOverlayState state,
        LocalPlayerController? localPlayer,
        Action? debugKillPlayer,
        Action? debugRespawnPlayer)
    {
        ImguiNative.igSetNextWindowSize(new Vector2(560.0f, 720.0f), ImGuiCond.FirstUseEver);
        if (ImguiNative.igBegin("Telemetry", null, ImGuiWindowFlags.None))
        {
            if (Section("Frame"))
                Text(state.FrameTimingText);

            if (Section("Renderer"))
                BuildRendererSection(state.Renderer);

            if (Section("Simulation"))
                BuildSimulationSection(state);

            if (Section("Player"))
                BuildPlayerSection(state.Player, localPlayer, debugKillPlayer, debugRespawnPlayer);

            if (Section("Physics"))
                BuildPhysicsSection(state.Physics);

            if (state.Server is TelemetryServerState server && Section("Server"))
                BuildServerSection(server);

            if (state.Network is TelemetryNetworkState network && Section("Network"))
                BuildNetworkSection(network);

            if (Section("Connection"))
                BuildConnectionSection(state.Connection);
        }

        ImguiNative.igEnd();
    }

    private static void BuildRendererSection(TelemetryRendererState? renderer)
    {
        if (renderer is null)
        {
            Text("Renderer telemetry unavailable");
            return;
        }

        Text($"Camera: active {renderer.ActiveCameraMode}; launch {renderer.LaunchCameraMode}");
        Text(renderer.LaunchPositionText);
        Text(renderer.LaunchLookAtText);
        Text($"Render view: {renderer.RenderViewMode}; mouse: {(renderer.MouseCaptured ? "captured" : "free")}");
        Text($"Map: {renderer.LoadedMapId}");
        Text($"Content: {renderer.StaticBoxCount} boxes; {renderer.StaticModelCount} models; {renderer.LoadedModelAssetCount} loaded assets");
        Text(renderer.ScreenshotStateText);
        Text(renderer.ScreenshotTargetFrame is int targetFrame
            ? $"Screenshot frames: {renderer.CompletedFrames} completed; target {targetFrame}"
            : $"Screenshot frames: {renderer.CompletedFrames} completed; target none");
        WrappedText(renderer.ScreenshotOutputPathText);
    }

    private static void BuildSimulationSection(ImGuiDebugOverlayState state)
    {
        Text(state.FixedTicksText);
        Text(state.TotalFixedTickText);

        TelemetrySimulationState simulation = state.Simulation;
        if (simulation.ServerTick is ulong serverTick)
        {
            Text($"Server tick: {serverTick}");
            Text($"Server - client tick: {simulation.ServerTickDifference:+#;-#;0}");
        }
        else
        {
            Text("Server tick: waiting for authoritative snapshot");
        }

        if (simulation.PendingInputCount is int pendingInputs)
        {
            Text($"Prediction pending inputs: {pendingInputs}");
            Text($"Prediction replayed inputs: {simulation.ReplayedInputCount}");
            Text($"Reconciliations: {simulation.ReconciliationCount}");
            Text(FormattableString.Invariant($"Last correction: {simulation.CorrectionDistance:0.000} m"));
        }
        else
        {
            Text("Prediction: not used in offline mode");
        }
    }

    private static void BuildPlayerSection(
        TelemetryPlayerState? player,
        LocalPlayerController? localPlayer,
        Action? debugKillPlayer,
        Action? debugRespawnPlayer)
    {
        if (player is null)
        {
            Text("Player telemetry unavailable");
            return;
        }

        Text(player.Status);
        if (player.Values is TelemetryPlayerValues values)
        {
            Text($"Source: {values.Source}");
            Text(values.PositionText);
            Text(values.VelocityText);
            Text(values.LookText);
            Text(values.HealthText);
            Text(values.AliveText);
            Text(values.StanceText);
            Text(values.SprintText);
            Text(values.GroundedText);
            Text(values.WeaponText);
            Text(values.AmmunitionText);
        }

        if (player.OfflineDiagnostics is PlayerDiagnosticsState offline)
        {
            Text(offline.LastShotText);
            Text(offline.HitMarkerText);
            Text(offline.HitIdentityText);
            Text(offline.DamageText);
            Text(offline.FeedbackLifetimeText);
        }

        if (localPlayer is null)
            return;

        if (ImguiNative.igButton("Kill Player", new Vector2(110.0f, 0.0f)))
        {
            if (debugKillPlayer is not null)
                debugKillPlayer();
            else
                localPlayer.DebugKill();
        }

        ImguiNative.igSameLine(0.0f, -1.0f);

        if (ImguiNative.igButton("Respawn Player", new Vector2(140.0f, 0.0f)))
            (debugRespawnPlayer ?? localPlayer.DebugRespawn)();
    }

    private static void BuildPhysicsSection(TelemetryPhysicsState? physics)
    {
        if (physics is null)
        {
            Text("Physics telemetry unavailable");
            return;
        }

        Text($"Mode: {physics.Mode}");
        Text(physics.CollisionWorldAvailable is bool collisionAvailable
            ? $"Collision world: {(collisionAvailable ? "available" : "unavailable")}"
            : "Collision world: waiting for connection acceptance");
        Text(physics.StaticColliderCount is int colliderCount
            ? $"Static colliders: {colliderCount}"
            : "Static colliders: unavailable");

        if (physics.PredictionActive is bool predictionActive)
        {
            Text($"Prediction active: {(predictionActive ? "yes" : "no")}");
            Text($"Prediction seeded: {(physics.PredictionSeeded == true ? "yes" : "no")}");
        }
        else
        {
            Text("Prediction: not applicable");
        }
    }

    private static void BuildServerSection(TelemetryServerState server)
    {
        Text(server.Status);
        if (server.Snapshot is not ServerSnapshot snapshot)
            return;

        Text($"Server tick: {snapshot.ServerTick}");
        Text($"Match: {snapshot.Match.Phase} (since tick {snapshot.Match.PhaseStartedTick})");
        Text($"Players: {snapshot.Players.Count}; living: {snapshot.Match.LivingPlayerCount}");
        Text(snapshot.Match.WinnerPlayerId is uint winnerId
            ? $"Winner player: {winnerId}"
            : "Winner: none");
        Text(FormattableString.Invariant(
            $"Safe zone center: ({snapshot.SafeZone.Center.X:0.00}, {snapshot.SafeZone.Center.Y:0.00}, {snapshot.SafeZone.Center.Z:0.00})"));
        Text(FormattableString.Invariant(
            $"Safe zone radius: {snapshot.SafeZone.CurrentRadius:0.00} -> {snapshot.SafeZone.TargetRadius:0.00} m"));
        Text($"Safe zone updated tick: {snapshot.SafeZone.LastUpdatedTick}");
    }

    private static void BuildNetworkSection(TelemetryNetworkState network)
    {
        ClientNetworkTelemetryValues client = network.Client;
        NetworkPeerStatistics? transport = network.Transport;

        if (client.OneWayLatencyMilliseconds is int latency)
            Text($"One-way latency: {latency} ms ({client.LatencySampleCount} samples)");
        else if (transport is NetworkPeerStatistics lastTransport)
            Text($"One-way latency: {lastTransport.OneWayLatencyMilliseconds} ms (transport)");
        else
            Text("One-way latency: waiting for sample");

        Text(client.LatencyJitterMilliseconds is double jitter
            ? FormattableString.Invariant($"Latency jitter: {jitter:0.00} ms")
            : "Latency jitter: waiting for consecutive samples");

        if (transport is NetworkPeerStatistics statistics)
        {
            Text($"RTT: {statistics.RoundTripTimeMilliseconds} ms");
            Text($"MTU: {statistics.MaximumTransmissionUnitBytes} bytes");
            Text(FormattableString.Invariant($"Time since packet: {statistics.TimeSinceLastPacketMilliseconds:0} ms"));
            Text($"Packets sent/received: {statistics.PacketsSent} / {statistics.PacketsReceived}");
            Text($"Bytes sent/received: {statistics.BytesSent} / {statistics.BytesReceived}");
            Text($"Packet loss: {statistics.PacketsLost} ({statistics.PacketLossPercent}%)");
        }
        else
        {
            Text("Transport statistics: unavailable");
        }

        Text($"Successful input sends: {client.SuccessfulInputSendCount}");
        Text($"Packets received by client: {client.ReceivedPacketCount}");
        Text($"Snapshot packets: {client.ReceivedSnapshotPacketCount}");
        Text($"Valid/invalid snapshots: {client.ValidSnapshotPacketCount} / {client.InvalidSnapshotPacketCount}");
        Text($"Network errors: {client.NetworkErrorCount}");
        Text($"Remote snapshot buffer: {network.RemoteSnapshotBufferCount}");
        Text($"Interpolation delay: {network.RemoteInterpolationDelayTicks} ticks");
        if (network.RemoteSnapshotBufferCount > 0)
        {
            Text(FormattableString.Invariant($"Interpolation target: {network.LastRemoteInterpolationTargetTick:0.00}"));
            Text($"Remote render: {(network.LastRemoteRenderUsedInterpolation ? "interpolated" : "fallback")}");
        }
        else
        {
            Text("Remote interpolation: waiting for snapshots");
        }
    }

    private static void BuildConnectionSection(TelemetryConnectionState? connection)
    {
        if (connection is null)
        {
            Text("Connection telemetry unavailable");
            return;
        }

        Text($"Mode: {connection.Mode}");
        Text(connection.Status);
        if (connection.Endpoint is NetworkEndpoint endpoint)
            Text($"Endpoint: {endpoint}");
        if (connection.PeerId is NetworkPeerId peerId)
            Text($"Peer: {peerId}");
        if (connection.HandshakeState is NetworkHandshakeClientState handshakeState)
            Text($"Handshake: {handshakeState}");
        if (connection.Rejection is ServerReject rejection)
            Text($"Rejection: {rejection.Reason} - {rejection.Detail}");

        if (connection.AcceptedSession is ServerAccept accepted)
        {
            Text($"Session: {accepted.SessionId}");
            Text($"Connection id: {accepted.ConnectionId}");
            Text($"Player id: {accepted.PlayerId}");
        }

        Text(connection.LastDisconnectReason is NetworkDisconnectReason disconnectReason
            ? $"Last disconnect: {disconnectReason}"
            : "Last disconnect: none");
        Text(connection.LastNetworkError is ClientNetworkErrorValues error
            ? $"Last socket error: {error.SocketError}{(error.Endpoint is NetworkEndpoint errorEndpoint ? $" at {errorEndpoint}" : string.Empty)}"
            : "Last socket error: none");
    }

    private static void BuildTrainingDummyDiagnostics(TrainingDummy trainingDummy)
    {
        TrainingDummyDiagnosticsState state = TrainingDummyDiagnosticsState.FromDummy(trainingDummy);

        ImguiNative.igSetNextWindowSize(new Vector2(620.0f, 320.0f), ImGuiCond.FirstUseEver);
        if (ImguiNative.igBegin("Training Dummy", null, ImGuiWindowFlags.None))
        {
            Text($"Id: {state.Id}");
            Text(state.HealthText);
            Text(state.AliveText);

            if (ImguiNative.igButton("Reset", new Vector2(80.0f, 0.0f)))
                trainingDummy.Reset();

            ImguiNative.igSeparator();
            Text(state.HistoryHeaderText);

            if (state.DamageHistory.Count == 0)
            {
                Text("No applied damage");
            }
            else
            {
                foreach (TrainingDummyDamageEntry entry in state.DamageHistory)
                    Text(TrainingDummyDiagnosticsState.FormatDamageEntry(entry));
            }
        }

        ImguiNative.igEnd();
    }

    private static bool Section(string label) =>
        ImguiNative.igCollapsingHeader_TreeNodeFlags(label, ImGuiTreeNodeFlags.DefaultOpen);

    private static void Text(string text) => ImguiNative.igTextUnformatted(text, null!);

    private static void WrappedText(string text)
    {
        ImguiNative.igPushTextWrapPos(0.0f);
        Text(text);
        ImguiNative.igPopTextWrapPos();
    }

}
