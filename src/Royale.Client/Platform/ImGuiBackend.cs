using System.Runtime.InteropServices;
using Evergine.Bindings.Imgui;
using Evergine.Mathematics;
using SDL;

namespace Royale.Client.Platform;

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

            if (!royale_imgui_sdl3_init_for_sdlgpu(window.Handle))
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

    public void EndFrame()
    {
        ThrowIfDisposed();
        ImguiNative.igSetCurrentContext(context);
        ImguiNative.igEndFrame();
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
}
