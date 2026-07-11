using SDL;
using Royale.Rendering.Meshes;
using static SDL.SDL3;

namespace Royale.Rendering.Platform;

public sealed unsafe class SdlGpuOffscreenTarget : IDisposable
{
    private SDL_GPUDevice* device;
    private SDL_GPUTexture* colorTexture;
    private SDL_GPUTexture* depthTexture;

    internal SdlGpuOffscreenTarget(SDL_GPUDevice* device, SDL_GPUTextureFormat colorFormat, int width, int height)
    {
        this.device = device;
        ColorFormat = colorFormat;
        Resize(width, height);
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public IntPtr NativeTextureHandle { get { ThrowIfDisposed(); return (IntPtr)colorTexture; } }

    internal SDL_GPUTexture* ColorTexture { get { ThrowIfDisposed(); return colorTexture; } }
    internal SDL_GPUTexture* DepthTexture { get { ThrowIfDisposed(); return depthTexture; } }
    internal SDL_GPUTextureFormat ColorFormat { get; }

    public void Resize(int width, int height)
    {
        ThrowIfDisposed();
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (Width == width && Height == height) return;
        ReleaseTextures();
        Width = width;
        Height = height;
        colorTexture = CreateTexture(ColorFormat, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET | SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER);
        depthTexture = CreateTexture(StaticMeshRenderer.DepthFormat, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET);
    }

    public void Dispose()
    {
        if (device is null) return;
        ReleaseTextures();
        device = null;
    }

    private SDL_GPUTexture* CreateTexture(SDL_GPUTextureFormat format, SDL_GPUTextureUsageFlags usage)
    {
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D, format = format, usage = usage,
            width = (uint)Width, height = (uint)Height, layer_count_or_depth = 1, num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        SDL_GPUTexture* texture = SDL_CreateGPUTexture(device, &info);
        if (texture is null) throw new InvalidOperationException($"SDL GPU offscreen texture creation failed: {SDL_GetError()}");
        return texture;
    }

    private void ReleaseTextures()
    {
        if (depthTexture is not null) SDL_ReleaseGPUTexture(device, depthTexture);
        if (colorTexture is not null) SDL_ReleaseGPUTexture(device, colorTexture);
        depthTexture = null;
        colorTexture = null;
    }

    private void ThrowIfDisposed()
    {
        if (device is null) throw new ObjectDisposedException(nameof(SdlGpuOffscreenTarget));
    }
}
