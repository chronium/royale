using System.Runtime.InteropServices;
using Evergine.Bindings.Imgui;
using Evergine.Mathematics;
using Royale.Rendering.Platform;
using SDL;
using Royale.Platform.Desktop;

namespace Royale.Rendering.UI;

public sealed unsafe class SdlGpuImGuiBackend : IDisposable
{
    private const string LibraryName = "royale_imgui";

    private readonly SdlWindow window;
    private readonly SdlGpuDevice gpuDevice;
    private IntPtr context;
    private bool sdl3Initialized;
    private bool sdlGpuInitialized;
    private IntPtr iniPathStorage;

    private SdlGpuImGuiBackend(SdlWindow window, SdlGpuDevice gpuDevice)
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

    public static SdlGpuImGuiBackend Create(
        SdlWindow window,
        SdlGpuDevice gpuDevice,
        SdlGpuImGuiSettings settings = default)
    {
        ImGuiNativeLibrary.ConfigureResolvers();

        var backend = new SdlGpuImGuiBackend(window, gpuDevice);

        try
        {
            backend.context = ImguiNative.igCreateContext(null);

            if (backend.context == IntPtr.Zero)
                throw new InvalidOperationException("ImGui context creation failed.");

            ImguiNative.igSetCurrentContext(backend.context);

            ImGuiIO* io = ImguiNative.igGetIO_Nil();
            if (io is null)
                throw new InvalidOperationException("ImGui IO was not available.");
            if (settings.EnableDocking)
                io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            if (!string.IsNullOrWhiteSpace(settings.IniPath))
            {
                backend.iniPathStorage = Marshal.StringToCoTaskMemUTF8(settings.IniPath);
                io->IniFilename = (byte*)backend.iniPathStorage;
            }

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

    public void SetMouseInputEnabled(bool enabled)
    {
        ThrowIfDisposed();
        ImguiNative.igSetCurrentContext(context);

        ImGuiIO* io = ImguiNative.igGetIO_Nil();
        if (io is null)
            throw new InvalidOperationException("ImGui IO was not available.");

        if (enabled)
            io->ConfigFlags &= ~ImGuiConfigFlags.NoMouse;
        else
            io->ConfigFlags |= ImGuiConfigFlags.NoMouse;
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
        if (iniPathStorage != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(iniPathStorage);
            iniPathStorage = IntPtr.Zero;
        }
    }

    private void ThrowIfDisposed()
    {
        if (context == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(SdlGpuImGuiBackend));
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
