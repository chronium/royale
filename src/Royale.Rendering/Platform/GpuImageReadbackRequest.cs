using SDL;
using static SDL.SDL3;

namespace Royale.Rendering.Platform;

public sealed unsafe class GpuImageReadbackRequest : IDisposable
{
    private SDL_GPUDevice* device;
    private SDL_GPUFence* fence;
    private SDL_GPUTransferBuffer* transfer;
    private readonly uint byteCount;
    private readonly SDL_GPUTextureFormat format;

    internal GpuImageReadbackRequest(SDL_GPUDevice* device, SDL_GPUFence* fence, SDL_GPUTransferBuffer* transfer, uint byteCount, int width, int height, SDL_GPUTextureFormat format)
    {
        this.device = device;
        this.fence = fence;
        this.transfer = transfer;
        this.byteCount = byteCount;
        this.format = format;
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public bool TryComplete(out GpuImageReadback? image)
    {
        ThrowIfDisposed();
        if (!SDL_QueryGPUFence(device, fence))
        {
            image = null;
            return false;
        }

        try
        {
            WaitForFence();
            image = MapImage();
            return true;
        }
        finally
        {
            Dispose();
        }
    }

    public GpuImageReadback Wait()
    {
        ThrowIfDisposed();
        try
        {
            WaitForFence();
            return MapImage();
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (device is null)
            return;
        if (fence is not null)
            SDL_ReleaseGPUFence(device, fence);
        if (transfer is not null)
            SDL_ReleaseGPUTransferBuffer(device, transfer);
        fence = null;
        transfer = null;
        device = null;
    }

    private GpuImageReadback MapImage()
    {
        IntPtr mapped = SDL_MapGPUTransferBuffer(device, transfer, cycle: false);
        if (mapped == IntPtr.Zero)
            throw new InvalidOperationException($"SDL GPU image transfer mapping failed: {SDL_GetError()}");
        try
        {
            byte[] rgba = GpuImageReadback.NormalizeToRgba(new ReadOnlySpan<byte>((void*)mapped, checked((int)byteCount)), format);
            return new GpuImageReadback(Width, Height, rgba);
        }
        finally
        {
            SDL_UnmapGPUTransferBuffer(device, transfer);
        }
    }

    private void WaitForFence()
    {
        SDL_GPUFence* value = fence;
        if (!SDL_WaitForGPUFences(device, wait_all: true, &value, 1))
            throw new InvalidOperationException($"SDL GPU image fence wait failed: {SDL_GetError()}");
    }

    private void ThrowIfDisposed()
    {
        if (device is null)
            throw new ObjectDisposedException(nameof(GpuImageReadbackRequest));
    }
}
