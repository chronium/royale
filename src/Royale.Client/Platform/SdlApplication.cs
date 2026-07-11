using System.Numerics;
using SDL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Royale.Client.Gameplay;
using Royale.Client.Input;
using Royale.Client.Launch;
using Royale.Client.Networking;
using Royale.Client.Presentation;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using Royale.Client.Timing;
using Royale.Client.UI;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Native;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;
using static SDL.SDL3;
using ZLogger;

namespace Royale.Client.Platform;

public sealed unsafe class SdlApplication : IDisposable
{
    private readonly record struct FrameTime(double DeltaSeconds);

    private readonly record struct FixedTickTime(double DeltaSeconds, ulong Tick);

    private const double FixedDeltaSeconds = 1.0 / 60.0;
    private const int MaxFixedTicksPerFrame = 4;
    private const int TitleUpdateIntervalMilliseconds = 500;
    private const int DefaultWindowWidth = 1920;
    private const int DefaultWindowHeight = 1080;

    private readonly InputState input = new();
    private readonly GameplayInputMapper gameplayInputMapper = new();
    private readonly DebugCamera freeCamera = DebugCamera.CreateDefault();
    private readonly GameplayView gameplayView = GameplayView.CreateDefault();
    private readonly PlayerEyeHeightSmoother playerEyeHeightSmoother = new();
    private readonly ClientCameraModeController cameraMode = new();
    private readonly RenderViewModeController renderViewMode;
    private readonly LocalPredictionSmoother networkPredictionSmoother = new();
    private readonly FixedUpdateAccumulator fixedTime = new(FixedDeltaSeconds, MaxFixedTicksPerFrame);
    private readonly TelemetryVisibilityController telemetryVisibility;
    private readonly ClientLaunchOptions options;
    private readonly ILogger<SdlApplication> logger;
    private bool initialized;
    private bool running;
    private int renderedFrames;
    private int framesSinceTitleUpdate;
    private double secondsSinceTitleUpdate;
    private int lastFixedTicksThisFrame;
    private SdlGpuDevice? gpuDevice;
    private ImGuiBackend? imguiBackend;
    private LocalPlayerController? localPlayer;
    private NetworkClientRuntime? networkClient;
    private GameMap? loadedMap;
    private StaticMeshAssetCache? staticMeshAssetCache;
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
        renderViewMode = new RenderViewModeController(options.RenderViewMode);
        telemetryVisibility = new TelemetryVisibilityController(options.TelemetryVisible);
        ApplyCameraLaunchOptions(options);
    }

    public SdlWindow? Window { get; private set; }

    public InputState Input => input;

    public ClientCameraMode CameraMode => cameraMode.Mode;

    public RenderCamera FreeCamera => freeCamera.ToRenderCamera();

    public RenderViewMode RenderViewMode => renderViewMode.Mode;

    public PlayerInputSample LastGameplayInput => lastGameplayInput;

    public LocalPlayerController? LocalPlayer => localPlayer;

    public bool TelemetryVisible => telemetryVisibility.Visible;

    public void Run()
    {
        Initialize();

        running = true;
        renderedFrames = 0;
        framesSinceTitleUpdate = 0;
        secondsSinceTitleUpdate = 0;

        ulong performanceFrequency = SDL_GetPerformanceFrequency();

        if (performanceFrequency == 0)
            throw new InvalidOperationException("SDL performance counter frequency is zero.");

        ulong previousCounter = SDL_GetPerformanceCounter();

        while (running)
        {
            ulong currentCounter = SDL_GetPerformanceCounter();
            double frameDeltaSeconds = (currentCounter - previousCounter) / (double)performanceFrequency;
            previousCounter = currentCounter;
            var frameTime = new FrameTime(frameDeltaSeconds);

            input.BeginFrame();
            ImGuiCaptureState imguiCapture = imguiBackend?.Capture ?? default;
            PollEvents(imguiCapture);
            UpdateCamera(frameTime);
            imguiBackend?.NewFrame(frameTime.DeltaSeconds);

            lastFixedTicksThisFrame = fixedTime.AddFrameTime(frameDeltaSeconds);
            ulong firstFixedTick = fixedTime.TotalFixedTicks - (ulong)lastFixedTicksThisFrame + 1;

            for (int tick = 0; tick < lastFixedTicksThisFrame; tick++)
                FixedUpdate(new FixedTickTime(FixedDeltaSeconds, firstFixedTick + (ulong)tick));

            if (imguiBackend is not null && telemetryVisibility.Visible)
            {
                imguiBackend.BuildDebugOverlay(
                    CreateTelemetryState(frameTime),
                    localPlayer,
                    DebugKillLocalPlayer,
                    DebugRespawnLocalPlayer);
            }
            Render(frameTime);
            UpdateWindowTitle(frameTime);

            SDL_Delay(1);
        }
    }

    private void FixedUpdate(FixedTickTime time)
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

    private void Render(FrameTime time)
    {
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

        gpuDevice?.PresentFrame(
            time.DeltaSeconds,
            renderCamera,
            renderViewMode.Mode,
            debugPrimitives,
            worldTextBillboards,
            imguiBackend,
            screenshotPath);

        localPlayer?.WeaponFeedback.Update(time.DeltaSeconds);

        if (screenshotPath is not null)
            running = false;
    }

    private void Initialize()
    {
        if (initialized)
            return;

        NativeLibraryResolver.ConfigureForAssembly(typeof(SDL3).Assembly);

        logger.ZLogInformation($"Initializing SDL video subsystem.");

        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL video initialization failed: {SDL_GetError()}");

        initialized = true;
        logger.ZLogInformation($"SDL video subsystem initialized.");

        logger.ZLogInformation($"Loading map {options.MapId}.");
        GameMap map = MapCatalog.LoadById(options.MapId);
        loadedMap = map;
        if (options.Mode == ClientLaunchMode.Offline)
            localPlayer = LocalPlayerController.Create(map);
        else
            networkClient = NetworkClientRuntime.Connect(options.ConnectHost!, options.Port);

        StaticMeshAssetCache assetCache = StaticMeshAssetCache.Load(AppContext.BaseDirectory);
        staticMeshAssetCache = assetCache;
        Dictionary<string, StaticMeshAsset> mapAssets = map.StaticModels
            .Select(model => model.AssetId)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(assetId => assetId, assetCache.GetRequired, StringComparer.Ordinal);
        StaticMeshScene staticMeshScene = MapStaticMeshScene.CreateScene(map, mapAssets);
        logger.ZLogInformation($"Loaded map {map.Id} with {map.StaticBoxes.Count} static boxes and {map.StaticModels.Count} static models.");

        logger.ZLogInformation($"Creating SDL window.");
        Window = SdlWindow.Create(
            "Royale",
            DefaultWindowWidth,
            DefaultWindowHeight,
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
        logger.ZLogInformation($"SDL window created.");

        logger.ZLogInformation($"Creating SDL GPU device.");
        gpuDevice = SdlGpuDevice.Create(Window, staticMeshScene);
        logger.ZLogInformation($"SDL GPU device created.");
        imguiBackend = ImGuiBackend.Create(Window, gpuDevice);
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

    private void PollEvents(ImGuiCaptureState imguiCapture)
    {
        SDL_Event sdlEvent;

        while (SDL_PollEvent(&sdlEvent))
        {
            imguiBackend?.ProcessEvent(&sdlEvent);
            HandleEvent(sdlEvent, imguiCapture);
        }
    }

    private void HandleEvent(SDL_Event sdlEvent, ImGuiCaptureState imguiCapture)
    {
        var inputOwnership = new GameInputOwnership(
            Window?.RelativeMouseMode.Enabled == true,
            imguiCapture);

        switch (sdlEvent.Type)
        {
            case SDL_EventType.SDL_EVENT_QUIT:
            case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                running = false;
                break;

            case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
            case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                Window?.RefreshSize();
                break;

            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                if (!sdlEvent.key.repeat)
                    HandleKeyDown(sdlEvent.key.key, inputOwnership);
                break;

            case SDL_EventType.SDL_EVENT_KEY_UP:
                if (input.IsKeyDown((int)sdlEvent.key.key) || inputOwnership.ShouldApplyKeyboardToGame(IsGlobalControl(sdlEvent.key.key)))
                    input.SetKeyUp((int)sdlEvent.key.key);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                if (inputOwnership.ShouldApplyMouseToGame())
                    input.SetMouseButtonDown((int)sdlEvent.button.button);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (input.IsMouseButtonDown((int)sdlEvent.button.button) || inputOwnership.ShouldApplyMouseToGame())
                    input.SetMouseButtonUp((int)sdlEvent.button.button);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                if (inputOwnership.ShouldApplyMouseToGame())
                    input.AddMouseDelta(sdlEvent.motion.xrel, sdlEvent.motion.yrel);
                break;
        }
    }

    private void HandleKeyDown(SDL_Keycode key, GameInputOwnership inputOwnership)
    {
        if (inputOwnership.ShouldApplyKeyboardToGame(IsGlobalControl(key)))
            input.SetKeyDown((int)key);

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
                running = false;
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

    private ImGuiDebugOverlayState CreateTelemetryState(FrameTime frameTime)
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
                fixedTime.TotalFixedTicks,
                renderer,
                networkClient);
        }

        if (localPlayer is not null)
        {
            return ImGuiDebugOverlayState.CreateOffline(
                frameTime.DeltaSeconds,
                lastFixedTicksThisFrame,
                fixedTime.TotalFixedTicks,
                renderer,
                localPlayer,
                localPlayer.CollisionWorld.ColliderCount);
        }

        return new ImGuiDebugOverlayState(
            frameTime.DeltaSeconds,
            lastFixedTicksThisFrame,
            fixedTime.TotalFixedTicks)
        {
            Renderer = renderer,
        };
    }

    private void UpdateCamera(FrameTime frameTime)
    {
        bool relativeMouseModeEnabled = Window?.RelativeMouseMode.Enabled == true;
        lastGameplayInput = gameplayInputMapper.FromInputState(
            input,
            relativeMouseModeEnabled,
            ownsGameplayInput: !cameraMode.IsFreecam);
        networkClient?.Poll();

        if (cameraMode.IsFreecam)
        {
            freeCamera.Update(
                DebugCameraInputMapper.FromInputState(input, relativeMouseModeEnabled),
                frameTime.DeltaSeconds);
            return;
        }

        if (localPlayer?.Alive == true)
            localPlayer.UpdateLook(lastGameplayInput);
        else
            networkClient?.ApplyLook(lastGameplayInput);
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

    private ServerSnapshot? CreateNetworkPresentationSnapshot(PlayerSnapshotState? networkPresentationPlayer, FrameTime time)
    {
        if (networkClient is null)
            return null;

        networkClient.AdvanceRemoteInterpolation(time.DeltaSeconds);

        return NetworkSnapshotPresentation.CreatePresentationSnapshot(
            networkClient.State,
            networkPresentationPlayer,
            networkClient.RemoteSnapshotInterpolator);
    }

    private PlayerSnapshotState? TryGetNetworkPresentationPlayer(FrameTime time)
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

    private void UpdateWindowTitle(FrameTime frameTime)
    {
        framesSinceTitleUpdate++;
        secondsSinceTitleUpdate += frameTime.DeltaSeconds;

        if (secondsSinceTitleUpdate * 1000.0 < TitleUpdateIntervalMilliseconds || Window is null)
            return;

        double fps = framesSinceTitleUpdate / secondsSinceTitleUpdate;
        Window.SetTitle(
            $"Royale - {fps:0} FPS - fixed {lastFixedTicksThisFrame} ticks/frame - tick {fixedTime.TotalFixedTicks} - mouse {(Window.RelativeMouseMode.Enabled ? "captured" : "free")} - view {renderViewMode.Mode}");

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

        Window?.Dispose();
        Window = null;

        if (initialized)
        {
            SDL_Quit();
            initialized = false;
        }

        logger.ZLogInformation($"Client shutdown complete.");
    }
}
