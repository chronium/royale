using System.Numerics;
using SDL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Royale.Client.Gameplay;
using Royale.Client.Input;
using Royale.Client.Launch;
using Royale.Client.Networking;
using Royale.Client.Presentation;
using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Debug;
using Royale.Rendering.Meshes;
using Royale.Rendering.Screenshots;
using Royale.Rendering.Text;
using Royale.Rendering.Platform;
using Royale.Rendering.UI;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Text;
using Royale.Client.UI;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Runtime;
using Royale.Content.Weapons;
using Royale.Platform.Desktop;
using Royale.Platform.Input;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;
using ZLogger;

namespace Royale.Client.Platform;

public sealed unsafe class SdlApplication : ISdlDesktopApplication, IDisposable
{
    private const double FixedDeltaSeconds = 1.0 / 60.0;
    private const int MaxFixedTicksPerFrame = 4;
    private const int TitleUpdateIntervalMilliseconds = 500;
    private const int DefaultWindowWidth = 1920;
    private const int DefaultWindowHeight = 1080;

    private readonly GameplayInputMapper gameplayInputMapper = new();
    private readonly DebugCamera freeCamera = DebugCamera.CreateDefault();
    private readonly GameplayView gameplayView = GameplayView.CreateDefault();
    private readonly PlayerEyeHeightSmoother playerEyeHeightSmoother = new();
    private readonly ClientCameraModeController cameraMode = new();
    private readonly RenderViewModeController renderViewMode;
    private readonly LocalPredictionSmoother networkPredictionSmoother = new();
    private readonly SdlDesktopHost host;
    private readonly TelemetryVisibilityController telemetryVisibility;
    private readonly ClientLaunchOptions options;
    private readonly ILogger<SdlApplication> logger;
    private int renderedFrames;
    private int framesSinceTitleUpdate;
    private double secondsSinceTitleUpdate;
    private int lastFixedTicksThisFrame;
    private SdlGpuDevice? gpuDevice;
    private SdlGpuImGuiBackend? imguiBackend;
    private LocalPlayerController? localPlayer;
    private NetworkClientRuntime? networkClient;
    private GameMap? loadedMap;
    private StaticMeshAssetCache? staticMeshAssetCache;
    private StaticMeshScene? staticMeshScene;
    private PlayerInputSample lastGameplayInput;

    public SdlApplication()
        : this(ClientLaunchOptions.Default)
    {
    }

    public SdlApplication(ClientLaunchOptions options)
        : this(options, NullLogger<SdlApplication>.Instance)
    {
    }

    public SdlApplication(ClientLaunchOptions options, ILogger<SdlApplication> logger)
    {
        this.options = options;
        this.logger = logger;
        host = new SdlDesktopHost(
            new SdlWindowSettings(
                "Royale",
                DefaultWindowWidth,
                DefaultWindowHeight,
                SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY),
            new SdlLoopSettings(FixedDeltaSeconds, MaxFixedTicksPerFrame, idleDelayMilliseconds: 1),
            logger);
        renderViewMode = new RenderViewModeController(options.RenderViewMode);
        telemetryVisibility = new TelemetryVisibilityController(options.TelemetryVisible);
        ApplyCameraLaunchOptions(options);
    }

    public SdlWindow? Window => host.Window;

    public InputState Input => host.Input;

    public ClientCameraMode CameraMode => cameraMode.Mode;

    public RenderCamera FreeCamera => freeCamera.ToRenderCamera();

    public RenderViewMode RenderViewMode => renderViewMode.Mode;

    public PlayerInputSample LastGameplayInput => lastGameplayInput;

    public LocalPlayerController? LocalPlayer => localPlayer;

    public bool TelemetryVisible => telemetryVisibility.Visible;

    public void Run()
    {
        renderedFrames = 0;
        framesSinceTitleUpdate = 0;
        secondsSinceTitleUpdate = 0;
        host.Run(this);
    }

    public void FixedUpdate(SdlFixedTickTime time)
    {
        if (networkClient is not null)
        {
            networkClient.FixedUpdate(lastGameplayInput, time.Tick);
            return;
        }

        if (localPlayer is null)
            return;

        bool wasAlive = localPlayer.Alive;

        if (cameraMode.ShouldApplyGameplayFixedUpdateForPlayer(localPlayer.Alive))
            localPlayer.FixedUpdate(lastGameplayInput, time.DeltaSeconds);

        cameraMode.HandleLocalPlayerAliveTransition(wasAlive, localPlayer.Alive);
    }

    public void Render(SdlFrameTime time)
    {
        lastFixedTicksThisFrame = host.LastFixedTicksThisFrame;
        if (imguiBackend is not null && telemetryVisibility.Visible)
        {
            ImGuiDebugOverlay.Build(
                CreateTelemetryState(time),
                localPlayer,
                DebugKillLocalPlayer,
                DebugRespawnLocalPlayer);
        }

        renderedFrames++;
        string? screenshotPath = options.ScreenshotPath is not null && renderedFrames == options.ScreenshotAfterFrames
            ? options.ScreenshotPath
            : null;

        PlayerSnapshotState? networkPresentationPlayer = TryGetNetworkPresentationPlayer(time);
        RenderCamera renderCamera;
        if (cameraMode.IsFreecam)
        {
            playerEyeHeightSmoother.Reset();
            renderCamera = freeCamera.ToRenderCamera();
        }
        else
        {
            renderCamera = CreateGameplayRenderCamera(networkPresentationPlayer, time.DeltaSeconds);
        }

        ServerSnapshot? networkPresentationSnapshot = CreateNetworkPresentationSnapshot(networkPresentationPlayer, time);
        DebugPrimitiveList? debugPrimitives = loadedMap is null
            ? null
            : DebugSceneBuilder.Build(
                loadedMap,
                localPlayer,
                networkPresentationSnapshot,
                networkClient?.PredictionCollisionWorld);

        IReadOnlyList<WorldTextBillboard>? worldTextBillboards = localPlayer is null
            ? null
            : WorldTextSmokeLabelState.CreateDefault(localPlayer.TrainingDummy.FeetPosition, localPlayer.TrainingDummy.Height).Labels;

        if (gpuDevice is not null && staticMeshScene is not null)
        {
            GpuImageReadback? image = gpuDevice.PresentFrame(
                new RenderFrame(renderCamera, staticMeshScene, renderViewMode.Mode, debugPrimitives, worldTextBillboards),
                imguiBackend,
                readback: screenshotPath is not null);
            if (image is not null && screenshotPath is not null)
                PngScreenshotWriter.Save(screenshotPath, image.RgbaBytes, image.Width, image.Height);
        }

        localPlayer?.WeaponFeedback.Update(time.DeltaSeconds);
        UpdateWindowTitle(time);

        if (screenshotPath is not null)
            host.RequestExit();
    }

    public void Initialize(SdlDesktopHost desktopHost)
    {
        if (!ReferenceEquals(host, desktopHost))
            throw new InvalidOperationException("The client was initialized by an unexpected SDL desktop host.");

        if (gpuDevice is not null)
            return;

        logger.ZLogInformation($"Loading map {options.MapId}.");
        RuntimeContentSelection content = RuntimeContentSelection.Load(
            options.MapId,
            options.MapFile,
            options.RequireMapIdMatch,
            options.AssetRoot);
        GameMap map = content.Map;
        loadedMap = map;
        if (options.Mode == ClientLaunchMode.Offline)
            localPlayer = LocalPlayerController.Create(map, assetRoot: content.AssetRoot);
        else
            networkClient = NetworkClientRuntime.Connect(
                options.ConnectHost!,
                options.Port,
                loadPredictionMap: requestedMapId => string.Equals(requestedMapId, map.Id, StringComparison.Ordinal)
                    ? map
                    : throw new InvalidDataException(
                        $"Server requested prediction map '{requestedMapId}', but the client loaded '{map.Id}'."),
                createPredictionCollisionWorld: predictionMap =>
                    MapStaticCollisionWorld.Create(predictionMap, content.AssetRoot));

        StaticMeshAssetCache assetCache = StaticMeshAssetCache.LoadAssetRoot(content.AssetRoot.FullName);
        staticMeshAssetCache = assetCache;
        Dictionary<string, StaticMeshAsset> mapAssets = map.StaticModels
            .Select(model => model.AssetId)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(assetId => assetId, assetCache.GetRequired, StringComparer.Ordinal);
        staticMeshScene = MapStaticMeshScene.CreateScene(map, mapAssets);
        logger.ZLogInformation($"Loaded map {map.Id} with {map.StaticBoxes.Count} static boxes and {map.StaticModels.Count} static models.");

        logger.ZLogInformation($"Creating SDL GPU device.");
        gpuDevice = SdlGpuDevice.Create(Window!);
        logger.ZLogInformation($"SDL GPU device created.");
        imguiBackend = SdlGpuImGuiBackend.Create(Window!, gpuDevice);
    }

    private void ApplyCameraLaunchOptions(ClientLaunchOptions launchOptions)
    {
        if (launchOptions.CameraMode == ClientCameraMode.Freecam)
            cameraMode.SwitchToFreecam();

        if (launchOptions.CameraPosition is Vector3 cameraPosition)
            freeCamera.Position = cameraPosition;

        if (launchOptions.CameraLookAt is Vector3 cameraLookAt)
            freeCamera.LookAt(cameraLookAt);
    }

    public void DebugKillLocalPlayer()
    {
        if (localPlayer is null)
            return;

        bool wasAlive = localPlayer.Alive;
        localPlayer.DebugKill();
        cameraMode.HandleLocalPlayerAliveTransition(wasAlive, localPlayer.Alive);
    }

    public void DebugRespawnLocalPlayer()
    {
        if (localPlayer is null)
            return;

        bool wasAlive = localPlayer.Alive;
        localPlayer.DebugRespawn();
        gameplayInputMapper.Reset();
        playerEyeHeightSmoother.Reset();
        cameraMode.HandleLocalPlayerAliveTransition(wasAlive, localPlayer.Alive);
    }

    public void ProcessEvent(in SDL_Event sdlEvent)
    {
        SDL_Event mutableEvent = sdlEvent;
        imguiBackend?.ProcessEvent(&mutableEvent);
        ImGuiCaptureState imguiCapture = imguiBackend?.Capture ?? default;
        var inputOwnership = new GameInputOwnership(
            Window?.RelativeMouseMode.Enabled == true,
            imguiCapture);

        switch (sdlEvent.Type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                if (!sdlEvent.key.repeat)
                    HandleKeyDown(sdlEvent.key.key, inputOwnership);
                break;

            case SDL_EventType.SDL_EVENT_KEY_UP:
                if (Input.IsKeyDown((int)sdlEvent.key.key) || inputOwnership.ShouldApplyKeyboardToGame(IsGlobalControl(sdlEvent.key.key)))
                    Input.SetKeyUp((int)sdlEvent.key.key);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                if (inputOwnership.ShouldApplyMouseToGame())
                    Input.SetMouseButtonDown((int)sdlEvent.button.button);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (Input.IsMouseButtonDown((int)sdlEvent.button.button) || inputOwnership.ShouldApplyMouseToGame())
                    Input.SetMouseButtonUp((int)sdlEvent.button.button);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                if (inputOwnership.ShouldApplyMouseToGame())
                    Input.AddMouseDelta(sdlEvent.motion.xrel, sdlEvent.motion.yrel);
                break;
        }
    }

    private void HandleKeyDown(SDL_Keycode key, GameInputOwnership inputOwnership)
    {
        if (inputOwnership.ShouldApplyKeyboardToGame(IsGlobalControl(key)))
            Input.SetKeyDown((int)key);

        switch (key)
        {
            case SDL_Keycode.SDLK_F1:
                Window?.RelativeMouseMode.Toggle();
                break;

            case SDL_Keycode.SDLK_F2:
                cameraMode.Toggle();
                break;

            case SDL_Keycode.SDLK_F3:
                telemetryVisibility.Toggle();
                break;

            case SDL_Keycode.SDLK_F5:
            case SDL_Keycode.SDLK_F6:
            case SDL_Keycode.SDLK_F7:
            case SDL_Keycode.SDLK_F8:
                renderViewMode.HandleKeyDown(key);
                break;

            case SDL_Keycode.SDLK_ESCAPE when Window?.RelativeMouseMode.Enabled == true:
                Window.RelativeMouseMode.SetEnabled(false);
                break;

            case SDL_Keycode.SDLK_ESCAPE:
                host.RequestExit();
                break;
        }
    }

    public static bool IsGlobalControl(SDL_Keycode key) =>
        key is SDL_Keycode.SDLK_F1
            or SDL_Keycode.SDLK_F2
            or SDL_Keycode.SDLK_F3
            or SDL_Keycode.SDLK_F5
            or SDL_Keycode.SDLK_F6
            or SDL_Keycode.SDLK_F7
            or SDL_Keycode.SDLK_F8
            or SDL_Keycode.SDLK_ESCAPE;

    private ImGuiDebugOverlayState CreateTelemetryState(SdlFrameTime frameTime)
    {
        bool mouseCaptured = Window?.RelativeMouseMode.Enabled == true;
        TelemetryRendererState? renderer = loadedMap is GameMap map && staticMeshAssetCache is StaticMeshAssetCache assetCache
            ? new TelemetryRendererState(
                cameraMode.Mode,
                options.CameraMode,
                options.CameraPosition,
                options.CameraLookAt,
                renderViewMode.Mode,
                mouseCaptured,
                map.Id,
                map.StaticBoxes.Count,
                map.StaticModels.Count,
                assetCache.LoadedAssetCount,
                options.ScreenshotPath is not null,
                options.ScreenshotPath is null ? null : options.ScreenshotAfterFrames,
                renderedFrames,
                options.ScreenshotPath)
            : null;

        if (networkClient is not null)
        {
            return ImGuiDebugOverlayState.CreateNetworked(
                frameTime.DeltaSeconds,
                lastFixedTicksThisFrame,
                host.TotalFixedTicks,
                renderer,
                networkClient);
        }

        if (localPlayer is not null)
        {
            return ImGuiDebugOverlayState.CreateOffline(
                frameTime.DeltaSeconds,
                lastFixedTicksThisFrame,
                host.TotalFixedTicks,
                renderer,
                localPlayer,
                localPlayer.CollisionWorld.ColliderCount);
        }

        return new ImGuiDebugOverlayState(
            frameTime.DeltaSeconds,
            lastFixedTicksThisFrame,
            host.TotalFixedTicks)
        {
            Renderer = renderer,
        };
    }

    public void Update(SdlFrameTime frameTime)
    {
        bool relativeMouseModeEnabled = Window?.RelativeMouseMode.Enabled == true;
        lastGameplayInput = gameplayInputMapper.FromInputState(
            Input,
            relativeMouseModeEnabled,
            ownsGameplayInput: !cameraMode.IsFreecam);
        networkClient?.Poll();

        if (cameraMode.IsFreecam)
        {
            freeCamera.Update(
                DebugCameraInputMapper.FromInputState(Input, relativeMouseModeEnabled),
                frameTime.DeltaSeconds);
        }
        else if (localPlayer?.Alive == true)
            localPlayer.UpdateLook(lastGameplayInput);
        else
            networkClient?.ApplyLook(lastGameplayInput);

        imguiBackend?.NewFrame(frameTime.DeltaSeconds);
    }

    private RenderCamera CreateGameplayRenderCamera(PlayerSnapshotState? networkPresentationPlayer, double deltaSeconds)
    {
        if (localPlayer is not null)
        {
            float target = localPlayer.ViewSettings.GetEyeHeight(localPlayer.CharacterState.Stance);
            float eyeHeight = playerEyeHeightSmoother.Update(target, deltaSeconds);
            return GameplayView.CreateRenderCamera(localPlayer.FeetPosition, localPlayer.LookState, eyeHeight);
        }

        if (networkClient is not null)
        {
            if (networkPresentationPlayer is not PlayerSnapshotState player)
            {
                playerEyeHeightSmoother.Reset();
                return NetworkSnapshotPresentation.CreateRenderCamera(
                    networkClient.State,
                    networkClient.LookState,
                    gameplayView,
                    networkPresentationPlayer);
            }

            KinematicCharacterStance stance = player.Crouched
                ? KinematicCharacterStance.Crouched
                : KinematicCharacterStance.Standing;
            float target = gameplayView.ViewSettings.GetEyeHeight(stance);
            float eyeHeight = playerEyeHeightSmoother.Update(target, deltaSeconds);
            return NetworkSnapshotPresentation.CreateRenderCamera(
                networkClient.State,
                networkClient.LookState,
                gameplayView,
                networkPresentationPlayer,
                eyeHeight);
        }

        playerEyeHeightSmoother.Reset();
        return gameplayView.ToRenderCamera(Vector3.Zero, new PlayerLookState(0.0f, 0.0f));
    }

    private ServerSnapshot? CreateNetworkPresentationSnapshot(PlayerSnapshotState? networkPresentationPlayer, SdlFrameTime time)
    {
        if (networkClient is null)
            return null;

        networkClient.AdvanceRemoteInterpolation(time.DeltaSeconds);

        return NetworkSnapshotPresentation.CreatePresentationSnapshot(
            networkClient.State,
            networkPresentationPlayer,
            networkClient.RemoteSnapshotInterpolator);
    }

    private PlayerSnapshotState? TryGetNetworkPresentationPlayer(SdlFrameTime time)
    {
        if (networkClient is null)
            return null;

        if (!networkClient.TryGetPredictedLocalPlayer(out PlayerSnapshotState predictedPlayer))
        {
            networkPredictionSmoother.Reset();
            gameplayInputMapper.Reset();
            playerEyeHeightSmoother.Reset();
            return null;
        }

        return networkPredictionSmoother.Update(
            predictedPlayer,
            networkClient.ReconciliationCount,
            networkClient.LastPredictionCorrectionDistance,
            time.DeltaSeconds);
    }

    private void UpdateWindowTitle(SdlFrameTime frameTime)
    {
        framesSinceTitleUpdate++;
        secondsSinceTitleUpdate += frameTime.DeltaSeconds;

        if (secondsSinceTitleUpdate * 1000.0 < TitleUpdateIntervalMilliseconds || Window is null)
            return;

        double fps = framesSinceTitleUpdate / secondsSinceTitleUpdate;
        Window.SetTitle(
            $"Royale - {fps:0} FPS - fixed {lastFixedTicksThisFrame} ticks/frame - tick {host.TotalFixedTicks} - mouse {(Window.RelativeMouseMode.Enabled ? "captured" : "free")} - view {renderViewMode.Mode}");

        framesSinceTitleUpdate = 0;
        secondsSinceTitleUpdate = 0;
    }

    public void Dispose()
    {
        logger.ZLogInformation($"Client shutdown beginning.");

        imguiBackend?.Dispose();
        imguiBackend = null;

        gpuDevice?.Dispose();
        gpuDevice = null;

        localPlayer?.Dispose();
        localPlayer = null;
        networkClient?.Dispose();
        networkClient = null;
        loadedMap = null;

        host.Dispose();

        logger.ZLogInformation($"Client shutdown complete.");
    }
}
