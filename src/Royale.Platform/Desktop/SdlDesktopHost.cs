using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Royale.Native;
using Royale.Platform.Input;
using Royale.Platform.Timing;
using SDL;
using static SDL.SDL3;
using ZLogger;

namespace Royale.Platform.Desktop;

public sealed unsafe class SdlDesktopHost : IDisposable
{
    private readonly SdlWindowSettings windowSettings;
    private readonly SdlLoopSettings loopSettings;
    private readonly FixedUpdateAccumulator fixedTime;
    private readonly ILogger logger;
    private bool initialized;
    private bool disposed;
    private bool exitRequested;

    public SdlDesktopHost(SdlWindowSettings windowSettings, SdlLoopSettings loopSettings)
        : this(windowSettings, loopSettings, NullLogger<SdlDesktopHost>.Instance)
    {
    }

    public SdlDesktopHost(
        SdlWindowSettings windowSettings,
        SdlLoopSettings loopSettings,
        ILogger logger)
    {
        this.windowSettings = windowSettings ?? throw new ArgumentNullException(nameof(windowSettings));
        this.loopSettings = loopSettings ?? throw new ArgumentNullException(nameof(loopSettings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        fixedTime = new FixedUpdateAccumulator(loopSettings.FixedDeltaSeconds, loopSettings.MaximumCatchUpTicks);
    }

    public SdlWindow? Window { get; private set; }
    public InputState Input { get; } = new();
    public bool IsInitialized => initialized;
    public bool IsExitRequested => exitRequested;
    public ulong TotalFixedTicks => fixedTime.TotalFixedTicks;
    public int LastFixedTicksThisFrame { get; private set; }

    public void Run(ISdlDesktopApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        ThrowIfDisposed();
        Initialize();
        exitRequested = false;
        application.Initialize(this);

        ulong performanceFrequency = SDL_GetPerformanceFrequency();
        if (performanceFrequency == 0)
            throw new InvalidOperationException("SDL performance counter frequency is zero.");

        ulong previousCounter = SDL_GetPerformanceCounter();
        while (!exitRequested)
        {
            ulong currentCounter = SDL_GetPerformanceCounter();
            double frameDeltaSeconds = (currentCounter - previousCounter) / (double)performanceFrequency;
            previousCounter = currentCounter;
            var frameTime = new SdlFrameTime(frameDeltaSeconds);

            Input.BeginFrame();
            PollEvents(application);
            application.Update(frameTime);

            LastFixedTicksThisFrame = fixedTime.AddFrameTime(frameDeltaSeconds);
            foreach (SdlFixedTickTime fixedTick in SdlFixedTickTime.ForFrame(
                         loopSettings.FixedDeltaSeconds,
                         fixedTime.TotalFixedTicks,
                         LastFixedTicksThisFrame))
                application.FixedUpdate(fixedTick);

            application.Render(frameTime);
            SDL_Delay(loopSettings.IdleDelayMilliseconds);
        }
    }

    public void RequestExit() => exitRequested = true;

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

        try
        {
            logger.ZLogInformation($"Creating SDL window.");
            Window = SdlWindow.Create(windowSettings);
            logger.ZLogInformation($"SDL window created.");
        }
        catch
        {
            SDL_Quit();
            initialized = false;
            throw;
        }
    }

    private void PollEvents(ISdlDesktopApplication application)
    {
        SDL_Event sdlEvent;
        while (SDL_PollEvent(&sdlEvent))
        {
            if (sdlEvent.Type is SDL_EventType.SDL_EVENT_WINDOW_RESIZED or SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED)
                Window?.RefreshSize();

            if (sdlEvent.Type is SDL_EventType.SDL_EVENT_QUIT or SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED)
                RequestExit();

            application.ProcessEvent(in sdlEvent);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        Window?.Dispose();
        Window = null;

        if (initialized)
        {
            SDL_Quit();
            initialized = false;
        }

        disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(SdlDesktopHost));
    }
}
