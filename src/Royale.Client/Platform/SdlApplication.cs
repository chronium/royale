using SDL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Royale.Client.Rendering;
using Royale.Native;
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

    private readonly InputState input = new();
    private readonly DebugCamera camera = DebugCamera.CreateDefault();
    private readonly FixedUpdateAccumulator fixedTime = new(FixedDeltaSeconds, MaxFixedTicksPerFrame);
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
    }

    public SdlWindow? Window { get; private set; }

    public InputState Input => input;

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

            imguiBackend?.BuildDebugOverlay(new ImGuiDebugOverlayState(
                frameTime.DeltaSeconds,
                lastFixedTicksThisFrame,
                fixedTime.TotalFixedTicks,
                Window?.RelativeMouseMode.Enabled == true));
            Render(frameTime);
            UpdateWindowTitle(frameTime);

            SDL_Delay(1);
        }
    }

    private static void FixedUpdate(FixedTickTime time)
    {
    }

    private void Render(FrameTime time)
    {
        renderedFrames++;
        string? screenshotPath = options.ScreenshotPath is not null && renderedFrames == options.ScreenshotAfterFrames
            ? options.ScreenshotPath
            : null;

        gpuDevice?.PresentFrame(time.DeltaSeconds, camera, imguiBackend, screenshotPath);

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

        logger.ZLogInformation($"Creating SDL window.");
        Window = SdlWindow.Create(
            "Royale",
            1280,
            720,
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
        logger.ZLogInformation($"SDL window created.");

        logger.ZLogInformation($"Creating SDL GPU device.");
        gpuDevice = SdlGpuDevice.Create(Window);
        logger.ZLogInformation($"SDL GPU device created.");
        imguiBackend = ImGuiBackend.Create(Window, gpuDevice);
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

            case SDL_Keycode.SDLK_ESCAPE when Window?.RelativeMouseMode.Enabled == true:
                Window.RelativeMouseMode.SetEnabled(false);
                break;

            case SDL_Keycode.SDLK_ESCAPE:
                running = false;
                break;
        }
    }

    private static bool IsGlobalControl(SDL_Keycode key) => key is SDL_Keycode.SDLK_F1 or SDL_Keycode.SDLK_ESCAPE;

    private void UpdateCamera(FrameTime frameTime) =>
        camera.Update(
            DebugCameraInputMapper.FromInputState(input, Window?.RelativeMouseMode.Enabled == true),
            frameTime.DeltaSeconds);

    private void UpdateWindowTitle(FrameTime frameTime)
    {
        framesSinceTitleUpdate++;
        secondsSinceTitleUpdate += frameTime.DeltaSeconds;

        if (secondsSinceTitleUpdate * 1000.0 < TitleUpdateIntervalMilliseconds || Window is null)
            return;

        double fps = framesSinceTitleUpdate / secondsSinceTitleUpdate;
        Window.SetTitle(
            $"Royale - {fps:0} FPS - fixed {lastFixedTicksThisFrame} ticks/frame - tick {fixedTime.TotalFixedTicks} - mouse {(Window.RelativeMouseMode.Enabled ? "captured" : "free")}");

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
