using System.Runtime.InteropServices;
using Evergine.Bindings.Imgui;
using Evergine.Mathematics;
using Royale.Client.Gameplay;
using Royale.Client.Platform;
using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using SDL;
using Royale.Platform.Desktop;

namespace Royale.Client.UI;

internal sealed unsafe class ImGuiBackend : IDisposable
{
    private const string LibraryName = "royale_imgui";

    private readonly SdlWindow window;
    private readonly SdlGpuDevice gpuDevice;
    private IntPtr context;
    private bool sdl3Initialized;
    private bool sdlGpuInitialized;

    private ImGuiBackend(SdlWindow window, SdlGpuDevice gpuDevice)
    {
        this.window = window;
        this.gpuDevice = gpuDevice;
    }

    public ImGuiCaptureState Capture
    {
        get
        {
            if (context == IntPtr.Zero)
                return default;

            ImGuiIO* io = ImguiNative.igGetIO_Nil();
            return io is null
                ? default
                : new ImGuiCaptureState(io->WantCaptureKeyboard != 0, io->WantCaptureMouse != 0);
        }
    }

    public static ImGuiBackend Create(SdlWindow window, SdlGpuDevice gpuDevice)
    {
        ImGuiNativeLibrary.ConfigureResolvers();

        var backend = new ImGuiBackend(window, gpuDevice);

        try
        {
            backend.context = ImguiNative.igCreateContext(null);

            if (backend.context == IntPtr.Zero)
                throw new InvalidOperationException("ImGui context creation failed.");

            ImguiNative.igSetCurrentContext(backend.context);

            if (!royale_imgui_sdl3_init_for_sdlgpu((SDL_Window*)window.NativeHandle))
                throw new InvalidOperationException("ImGui SDL3 platform backend initialization failed.");

            backend.sdl3Initialized = true;

            if (!royale_imgui_sdlgpu3_init(gpuDevice.Handle, (int)gpuDevice.GetSwapchainTextureFormat()))
                throw new InvalidOperationException("ImGui SDL_GPU renderer backend initialization failed.");

            backend.sdlGpuInitialized = true;
            return backend;
        }
        catch
        {
            backend.Dispose();
            throw;
        }
    }

    public void ProcessEvent(SDL_Event* sdlEvent)
    {
        ThrowIfDisposed();
        royale_imgui_sdl3_process_event(sdlEvent);
    }

    public void NewFrame(double deltaSeconds)
    {
        ThrowIfDisposed();

        ImguiNative.igSetCurrentContext(context);

        ImGuiIO* io = ImguiNative.igGetIO_Nil();

        if (io is null)
            throw new InvalidOperationException("ImGui IO was not available.");

        io->DisplaySize = new Vector2(window.Width, window.Height);
        io->DisplayFramebufferScale = new Vector2(
            window.Width == 0 ? 1.0f : window.PixelWidth / (float)window.Width,
            window.Height == 0 ? 1.0f : window.PixelHeight / (float)window.Height);
        io->DeltaTime = deltaSeconds > 0 ? (float)deltaSeconds : 1.0f / 60.0f;

        royale_imgui_sdlgpu3_new_frame();
        royale_imgui_sdl3_new_frame();
        ImguiNative.igNewFrame();
    }

    public void BuildDebugOverlay(
        ImGuiDebugOverlayState state,
        LocalPlayerController? localPlayer = null,
        Action? debugKillPlayer = null,
        Action? debugRespawnPlayer = null)
    {
        ThrowIfDisposed();
        ImguiNative.igSetCurrentContext(context);

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

    internal void Render(SDL_GPUCommandBuffer* commandBuffer, SDL_GPUTexture* swapchainTexture)
    {
        ThrowIfDisposed();
        ImguiNative.igSetCurrentContext(context);
        ImguiNative.igRender();

        ImDrawData* drawData = ImguiNative.igGetDrawData();

        if (drawData is null)
            return;

        royale_imgui_sdlgpu3_prepare_draw_data(drawData, commandBuffer);

        var colorTarget = new SDL_GPUColorTargetInfo
        {
            texture = swapchainTexture,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_LOAD,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
        };

        SDL_GPURenderPass* renderPass = SDL.SDL3.SDL_BeginGPURenderPass(commandBuffer, &colorTarget, 1, null);

        if (renderPass is null)
            throw new InvalidOperationException($"SDL GPU ImGui render pass creation failed: {SDL.SDL3.SDL_GetError()}");

        royale_imgui_sdlgpu3_render_draw_data(drawData, commandBuffer, renderPass);
        SDL.SDL3.SDL_EndGPURenderPass(renderPass);
    }

    public void Dispose()
    {
        if (sdlGpuInitialized)
        {
            royale_imgui_sdlgpu3_shutdown();
            sdlGpuInitialized = false;
        }

        if (sdl3Initialized)
        {
            royale_imgui_sdl3_shutdown();
            sdl3Initialized = false;
        }

        if (context != IntPtr.Zero)
        {
            ImguiNative.igDestroyContext(context);
            context = IntPtr.Zero;
        }
    }

    private void ThrowIfDisposed()
    {
        if (context == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(ImGuiBackend));
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool royale_imgui_sdl3_init_for_sdlgpu(SDL.SDL_Window* window);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool royale_imgui_sdl3_process_event(SDL_Event* sdlEvent);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void royale_imgui_sdl3_new_frame();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void royale_imgui_sdl3_shutdown();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool royale_imgui_sdlgpu3_init(SDL.SDL_GPUDevice* device, int colorTargetFormat);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void royale_imgui_sdlgpu3_new_frame();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void royale_imgui_sdlgpu3_shutdown();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void royale_imgui_sdlgpu3_prepare_draw_data(ImDrawData* drawData, SDL_GPUCommandBuffer* commandBuffer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void royale_imgui_sdlgpu3_render_draw_data(ImDrawData* drawData, SDL_GPUCommandBuffer* commandBuffer, SDL_GPURenderPass* renderPass);
}
