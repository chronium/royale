using SDL;
using static SDL.SDL3;

namespace Royale.Client.Platform;

public sealed unsafe class SdlGpuDevice : IDisposable
{
    public const SDL_GPUShaderFormat RequestedShaderFormats =
        SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV |
        SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL |
        SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL;

    private readonly SdlWindow window;
    private SDL_GPUDevice* device;
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

    public void PresentClearFrame()
    {
        ThrowIfDisposed();

        SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(device);

        if (commandBuffer is null)
            throw new InvalidOperationException($"SDL GPU command buffer acquisition failed: {SDL_GetError()}");

        SDL_GPUTexture* swapchainTexture;
        uint swapchainWidth;
        uint swapchainHeight;

        if (!SDL_WaitAndAcquireGPUSwapchainTexture(commandBuffer, window.Handle, &swapchainTexture, &swapchainWidth, &swapchainHeight))
            throw new InvalidOperationException($"SDL GPU swapchain texture acquisition failed: {SDL_GetError()}");

        if (swapchainTexture is not null)
        {
            window.UpdatePixelSize((int)swapchainWidth, (int)swapchainHeight);

            var colorTarget = new SDL_GPUColorTargetInfo
            {
                texture = swapchainTexture,
                clear_color = new SDL_FColor
                {
                    r = 0.03f,
                    g = 0.04f,
                    b = 0.06f,
                    a = 1.0f,
                },
                load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
            };

            SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(commandBuffer, &colorTarget, 1, depth_stencil_target_info: null);

            if (renderPass is null)
                throw new InvalidOperationException($"SDL GPU render pass creation failed: {SDL_GetError()}");

            SDL_EndGPURenderPass(renderPass);
        }

        if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
            throw new InvalidOperationException($"SDL GPU command buffer submission failed: {SDL_GetError()}");
    }

    public void Dispose()
    {
        if (device is null)
            return;

        if (windowClaimed)
        {
            SDL_ReleaseWindowFromGPUDevice(device, window.Handle);
            windowClaimed = false;
        }

        SDL_DestroyGPUDevice(device);
        device = null;
    }

    private void ClaimWindow()
    {
        if (!SDL_ClaimWindowForGPUDevice(device, window.Handle))
            throw new InvalidOperationException($"SDL GPU window claim failed: {SDL_GetError()}");

        windowClaimed = true;
    }

    private void ThrowIfDisposed()
    {
        if (device is null)
            throw new ObjectDisposedException(nameof(SdlGpuDevice));
    }
}
