using SDL;
using static SDL.SDL3;

namespace Royale.Rendering.Platform;

public sealed unsafe class SdlGpuSampledTexture : IDisposable
{
    private SDL_GPUDevice* device;
    private SDL_GPUTexture* texture;

    internal SdlGpuSampledTexture(SDL_GPUDevice* device, SDL_GPUTexture* texture, int width, int height)
    {
        this.device = device;
        this.texture = texture;
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    public nint NativeTextureHandle => (nint)texture;

    public void Dispose()
    {
        if (texture is null)
            return;
        SDL_ReleaseGPUTexture(device, texture);
        texture = null;
        device = null;
    }
}
