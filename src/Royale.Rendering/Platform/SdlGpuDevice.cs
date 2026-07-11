using SDL;
using Royale.Platform.Desktop;
using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Debug;
using Royale.Rendering.Meshes;
using Royale.Rendering.Text;
using Royale.Rendering.UI;
using static SDL.SDL3;

namespace Royale.Rendering.Platform;

public sealed unsafe class SdlGpuDevice : IDisposable
{
    public const SDL_GPUShaderFormat RequestedShaderFormats =
        SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV |
        SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL |
        SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL;

    private readonly SdlWindow window;
    private readonly List<SdlGpuOffscreenTarget> offscreenTargets = [];
    private SDL_GPUDevice* device;
    private StaticMeshRenderer? staticMeshRenderer;
    private DebugLineRenderer? debugLineRenderer;
    private BlurgTextRenderer? blurgTextRenderer;
    private bool windowClaimed;

    private SdlGpuDevice(SDL_GPUDevice* device, SdlWindow window)
    {
        this.device = device;
        this.window = window;
        SupportedShaderFormats = SDL_GetGPUShaderFormats(device);
        PreferredShaderFormat = SelectPreferredShaderFormat(SupportedShaderFormats);
    }

    public SDL_GPUShaderFormat SupportedShaderFormats { get; }

    public SDL_GPUShaderFormat? PreferredShaderFormat { get; }

    internal SDL_GPUDevice* Handle
    {
        get
        {
            ThrowIfDisposed();
            return device;
        }
    }

    public static SdlGpuDevice Create(SdlWindow window)
    {
        SDL_GPUDevice* device = SDL_CreateGPUDevice(RequestedShaderFormats, debug_mode: false, name: (byte*)null);

        if (device is null)
            throw new InvalidOperationException($"SDL GPU device creation failed: {SDL_GetError()}");

        var gpuDevice = new SdlGpuDevice(device, window);

        try
        {
            gpuDevice.ClaimWindow();
            return gpuDevice;
        }
        catch
        {
            gpuDevice.Dispose();
            throw;
        }
    }

    public static SDL_GPUShaderFormat? SelectPreferredShaderFormat(SDL_GPUShaderFormat supportedFormats)
    {
        if (supportedFormats.HasFlag(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL))
            return SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL;

        if (supportedFormats.HasFlag(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV))
            return SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV;

        if (supportedFormats.HasFlag(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL))
            return SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL;

        return null;
    }

    internal SDL_GPUTextureFormat GetSwapchainTextureFormat()
    {
        ThrowIfDisposed();
        return SDL_GetGPUSwapchainTextureFormat(device, (SDL_Window*)window.NativeHandle);
    }

    public SdlGpuOffscreenTarget CreateOffscreenTarget(int width, int height)
    {
        ThrowIfDisposed();
        var target = new SdlGpuOffscreenTarget(device, GetSwapchainTextureFormat(), width, height);
        offscreenTargets.Add(target);
        return target;
    }

    public GpuImageReadback? PresentFrame(
        RenderFrame frame,
        SdlGpuImGuiBackend? imguiBackend = null,
        bool readback = false)
    {
        ThrowIfDisposed();

        if (PreferredShaderFormat is null)
            throw new InvalidOperationException($"SDL GPU device does not support any requested shader format. Supported formats: {SupportedShaderFormats}");

        SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(device);

        if (commandBuffer is null)
            throw new InvalidOperationException($"SDL GPU command buffer acquisition failed: {SDL_GetError()}");

        SDL_GPUTexture* swapchainTexture;
        uint swapchainWidth;
        uint swapchainHeight;

        if (!SDL_WaitAndAcquireGPUSwapchainTexture(commandBuffer, (SDL_Window*)window.NativeHandle, &swapchainTexture, &swapchainWidth, &swapchainHeight))
            throw new InvalidOperationException($"SDL GPU swapchain texture acquisition failed: {SDL_GetError()}");

        SDL_GPUTextureFormat swapchainFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_INVALID;
        SDL_GPUTransferBuffer* screenshotTransferBuffer = null;
        uint screenshotByteCount = 0;

        if (swapchainTexture is not null)
        {
            window.UpdatePixelSize((int)swapchainWidth, (int)swapchainHeight);
            swapchainFormat = SDL_GetGPUSwapchainTextureFormat(device, (SDL_Window*)window.NativeHandle);
            staticMeshRenderer ??= new StaticMeshRenderer(
                Handle,
                swapchainFormat,
                PreferredShaderFormat.Value);
            blurgTextRenderer ??= new BlurgTextRenderer(
                Handle,
                swapchainFormat,
                PreferredShaderFormat.Value);
            staticMeshRenderer.PrepareScene(frame.StaticScene);
            if (frame.RenderViewMode.ShouldRenderDebugWireframes())
            {
                debugLineRenderer ??= new DebugLineRenderer(
                    Handle,
                    swapchainFormat,
                    PreferredShaderFormat.Value,
                    StaticMeshRenderer.DepthFormat);
                debugLineRenderer.Upload(commandBuffer, frame.DebugPrimitives ?? new DebugPrimitiveList());
            }

            var colorTarget = new SDL_GPUColorTargetInfo
            {
                texture = swapchainTexture,
                clear_color = frame.EffectiveClearColor,
                load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
            };
            SDL_GPUDepthStencilTargetInfo depthTarget = staticMeshRenderer.GetDepthTarget(swapchainWidth, swapchainHeight);

            SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(commandBuffer, &colorTarget, 1, &depthTarget);

            if (renderPass is null)
                throw new InvalidOperationException($"SDL GPU render pass creation failed: {SDL_GetError()}");

            if (frame.RenderViewMode.ShouldRenderWorldSolids() || frame.RenderViewMode.ShouldRenderCollisionSolids())
                staticMeshRenderer.Render(commandBuffer, renderPass, swapchainWidth, swapchainHeight, frame.Camera, frame.StaticScene);

            if (frame.RenderViewMode.ShouldRenderDebugWireframes())
                debugLineRenderer?.Render(commandBuffer, renderPass, swapchainWidth, swapchainHeight, frame.Camera);
            SDL_EndGPURenderPass(renderPass);

            blurgTextRenderer.RenderLabels(commandBuffer, swapchainTexture, swapchainWidth, swapchainHeight, frame.Camera, frame.WorldText);

            imguiBackend?.Render(commandBuffer, swapchainTexture);

            if (readback)
            {
                screenshotByteCount = checked(swapchainWidth * swapchainHeight * 4);
                screenshotTransferBuffer = CreateScreenshotTransferBuffer(screenshotByteCount);
                DownloadSwapchainTexture(commandBuffer, swapchainTexture, screenshotTransferBuffer, swapchainWidth, swapchainHeight);
            }
        }

        if (screenshotTransferBuffer is null)
        {
            if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
                throw new InvalidOperationException($"SDL GPU command buffer submission failed: {SDL_GetError()}");

            return null;
        }

        return SubmitReadback(commandBuffer, screenshotTransferBuffer, screenshotByteCount, swapchainWidth, swapchainHeight, swapchainFormat);
    }

    public GpuImageReadback? RenderOffscreen(
        SdlGpuOffscreenTarget target,
        RenderFrame frame,
        SdlGpuImGuiBackend? imguiBackend = null,
        bool readback = false)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(target);
        if (PreferredShaderFormat is null)
            throw new InvalidOperationException($"SDL GPU device does not support any requested shader format. Supported formats: {SupportedShaderFormats}");

        SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(device);
        if (commandBuffer is null)
            throw new InvalidOperationException($"SDL GPU command buffer acquisition failed: {SDL_GetError()}");

        uint width = (uint)target.Width;
        uint height = (uint)target.Height;
        staticMeshRenderer ??= new StaticMeshRenderer(Handle, target.ColorFormat, PreferredShaderFormat.Value);
        staticMeshRenderer.PrepareScene(frame.StaticScene);
        blurgTextRenderer ??= new BlurgTextRenderer(Handle, target.ColorFormat, PreferredShaderFormat.Value);
        if (frame.RenderViewMode.ShouldRenderDebugWireframes())
        {
            debugLineRenderer ??= new DebugLineRenderer(Handle, target.ColorFormat, PreferredShaderFormat.Value, StaticMeshRenderer.DepthFormat);
            debugLineRenderer.Upload(commandBuffer, frame.DebugPrimitives ?? new DebugPrimitiveList());
        }

        var colorTarget = new SDL_GPUColorTargetInfo
        {
            texture = target.ColorTexture,
            clear_color = frame.EffectiveClearColor,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
        };
        var depthTarget = new SDL_GPUDepthStencilTargetInfo
        {
            texture = target.DepthTexture, clear_depth = 1.0f,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR, store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
            stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE, stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
        };
        SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(commandBuffer, &colorTarget, 1, &depthTarget);
        if (renderPass is null)
            throw new InvalidOperationException($"SDL GPU offscreen render pass creation failed: {SDL_GetError()}");
        if (frame.RenderViewMode.ShouldRenderWorldSolids() || frame.RenderViewMode.ShouldRenderCollisionSolids())
            staticMeshRenderer.Render(commandBuffer, renderPass, width, height, frame.Camera, frame.StaticScene);
        if (frame.RenderViewMode.ShouldRenderDebugWireframes())
            debugLineRenderer?.Render(commandBuffer, renderPass, width, height, frame.Camera);
        SDL_EndGPURenderPass(renderPass);
        blurgTextRenderer.RenderLabels(commandBuffer, target.ColorTexture, width, height, frame.Camera, frame.WorldText);
        imguiBackend?.Render(commandBuffer, target.ColorTexture);

        if (!readback)
        {
            if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
                throw new InvalidOperationException($"SDL GPU command buffer submission failed: {SDL_GetError()}");
            return null;
        }

        uint byteCount = checked(width * height * 4);
        SDL_GPUTransferBuffer* transferBuffer = CreateScreenshotTransferBuffer(byteCount);
        DownloadSwapchainTexture(commandBuffer, target.ColorTexture, transferBuffer, width, height);
        return SubmitReadback(commandBuffer, transferBuffer, byteCount, width, height, target.ColorFormat);
    }

    public void Dispose()
    {
        if (device is null)
            return;

        staticMeshRenderer?.Dispose();
        staticMeshRenderer = null;

        debugLineRenderer?.Dispose();
        debugLineRenderer = null;

        blurgTextRenderer?.Dispose();
        blurgTextRenderer = null;

        foreach (SdlGpuOffscreenTarget target in offscreenTargets)
            target.Dispose();
        offscreenTargets.Clear();

        if (windowClaimed)
        {
            SDL_ReleaseWindowFromGPUDevice(device, (SDL_Window*)window.NativeHandle);
            windowClaimed = false;
        }

        SDL_DestroyGPUDevice(device);
        device = null;
    }

    private void ClaimWindow()
    {
        if (!SDL_ClaimWindowForGPUDevice(device, (SDL_Window*)window.NativeHandle))
            throw new InvalidOperationException($"SDL GPU window claim failed: {SDL_GetError()}");

        windowClaimed = true;
    }

    private void ThrowIfDisposed()
    {
        if (device is null)
            throw new ObjectDisposedException(nameof(SdlGpuDevice));
    }

    private SDL_GPUTransferBuffer* CreateScreenshotTransferBuffer(uint byteCount)
    {
        var createInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_DOWNLOAD,
            size = byteCount,
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(device, &createInfo);

        if (transferBuffer is null)
            throw new InvalidOperationException($"SDL GPU screenshot transfer buffer creation failed: {SDL_GetError()}");

        return transferBuffer;
    }

    private static void DownloadSwapchainTexture(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPUTexture* swapchainTexture,
        SDL_GPUTransferBuffer* transferBuffer,
        uint width,
        uint height)
    {
        SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);

        if (copyPass is null)
            throw new InvalidOperationException($"SDL GPU screenshot copy pass creation failed: {SDL_GetError()}");

        var source = new SDL_GPUTextureRegion
        {
            texture = swapchainTexture,
            w = width,
            h = height,
            d = 1,
        };
        var destination = new SDL_GPUTextureTransferInfo
        {
            transfer_buffer = transferBuffer,
            pixels_per_row = width,
            rows_per_layer = height,
        };

        SDL_DownloadFromGPUTexture(copyPass, &source, &destination);
        SDL_EndGPUCopyPass(copyPass);
    }

    private GpuImageReadback SubmitReadback(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPUTransferBuffer* transferBuffer,
        uint byteCount,
        uint width,
        uint height,
        SDL_GPUTextureFormat format)
    {
        try
        {
            SDL_GPUFence* fence = SDL_SubmitGPUCommandBufferAndAcquireFence(commandBuffer);

            if (fence is null)
                throw new InvalidOperationException($"SDL GPU screenshot command buffer submission failed: {SDL_GetError()}");

            try
            {
                SDL_GPUFence* fenceValue = fence;

                if (!SDL_WaitForGPUFences(device, wait_all: true, &fenceValue, 1))
                    throw new InvalidOperationException($"SDL GPU screenshot fence wait failed: {SDL_GetError()}");
            }
            finally
            {
                SDL_ReleaseGPUFence(device, fence);
            }

            IntPtr mapped = SDL_MapGPUTransferBuffer(device, transferBuffer, cycle: false);

            if (mapped == IntPtr.Zero)
                throw new InvalidOperationException($"SDL GPU screenshot transfer buffer mapping failed: {SDL_GetError()}");

            try
            {
                byte[] rgba = GpuImageReadback.NormalizeToRgba(
                    new ReadOnlySpan<byte>((void*)mapped, checked((int)byteCount)),
                    format);
                return new GpuImageReadback(checked((int)width), checked((int)height), rgba);
            }
            finally
            {
                SDL_UnmapGPUTransferBuffer(device, transferBuffer);
            }
        }
        finally
        {
            SDL_ReleaseGPUTransferBuffer(device, transferBuffer);
        }
    }
}
