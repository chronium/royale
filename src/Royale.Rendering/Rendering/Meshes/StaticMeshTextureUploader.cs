using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace Royale.Rendering.Meshes;

internal sealed unsafe class UploadedStaticMeshTexture : IDisposable
{
    private readonly SDL_GPUDevice* device;

    public UploadedStaticMeshTexture(SDL_GPUDevice* device, SDL_GPUTexture* texture)
    {
        this.device = device;
        Texture = texture;
    }

    public SDL_GPUTexture* Texture { get; private set; }

    public void Dispose()
    {
        if (Texture is null)
            return;

        SDL_ReleaseGPUTexture(device, Texture);
        Texture = null;
    }
}

internal static unsafe class StaticMeshTextureUploader
{
    private const SDL_GPUTextureFormat TextureFormat =
        SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM_SRGB;

    public static UploadedStaticMeshTexture UploadWhite(SDL_GPUDevice* device) =>
        UploadPixels(device, [255, 255, 255, 255], width: 1, height: 1, "white fallback");

    public static UploadedStaticMeshTexture Upload(SDL_GPUDevice* device, StaticMeshTextureData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Data.Length == 0)
            throw new InvalidDataException($"Static mesh texture '{source.DebugName}' is empty.");

        StaticMeshTexturePixels pixels = Decode(source);
        return UploadPixels(device, pixels.Rgba, pixels.Width, pixels.Height, source.DebugName);
    }

    private static StaticMeshTexturePixels Decode(StaticMeshTextureData source)
    {
        fixed (byte* encoded = source.Data)
        {
            SDL_IOStream* io = SDL_IOFromConstMem((IntPtr)encoded, (nuint)source.Data.Length);
            if (io is null)
                throw new InvalidOperationException($"SDL could not create an IO stream for texture '{source.DebugName}': {SDL_GetError()}");

            SDL_Surface* loaded = SDL_LoadSurface_IO(io, closeio: true);
            if (loaded is null)
                throw new InvalidDataException($"SDL could not decode texture '{source.DebugName}' ({source.MimeType}): {SDL_GetError()}");

            try
            {
                SDL_Surface* converted = SDL_ConvertSurface(loaded, SDL_PIXELFORMAT_RGBA32);
                if (converted is null)
                    throw new InvalidOperationException($"SDL could not convert texture '{source.DebugName}' to RGBA32: {SDL_GetError()}");

                try
                {
                    if (converted->w <= 0 || converted->h <= 0 || converted->pixels == IntPtr.Zero)
                        throw new InvalidDataException($"SDL decoded texture '{source.DebugName}' with invalid dimensions or pixels.");

                    int rowBytes = checked(converted->w * 4);
                    byte[] rgba = new byte[checked(rowBytes * converted->h)];
                    for (int row = 0; row < converted->h; row++)
                    {
                        IntPtr sourceRow = IntPtr.Add(converted->pixels, checked(row * converted->pitch));
                        Marshal.Copy(sourceRow, rgba, checked(row * rowBytes), rowBytes);
                    }

                    return new StaticMeshTexturePixels(rgba, checked((uint)converted->w), checked((uint)converted->h));
                }
                finally
                {
                    SDL_DestroySurface(converted);
                }
            }
            finally
            {
                SDL_DestroySurface(loaded);
            }
        }
    }

    private static UploadedStaticMeshTexture UploadPixels(
        SDL_GPUDevice* device,
        byte[] rgba,
        uint width,
        uint height,
        string debugName)
    {
        var textureInfo = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = TextureFormat,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = width,
            height = height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        SDL_GPUTexture* texture = SDL_CreateGPUTexture(device, &textureInfo);
        if (texture is null)
            throw new InvalidOperationException($"SDL GPU texture creation failed for '{debugName}': {SDL_GetError()}");

        uint byteCount = checked((uint)rgba.Length);
        var transferInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = byteCount,
        };
        SDL_GPUTransferBuffer* transfer = SDL_CreateGPUTransferBuffer(device, &transferInfo);
        if (transfer is null)
        {
            SDL_ReleaseGPUTexture(device, texture);
            throw new InvalidOperationException($"SDL GPU texture transfer-buffer creation failed for '{debugName}': {SDL_GetError()}");
        }

        try
        {
            IntPtr mapped = SDL_MapGPUTransferBuffer(device, transfer, cycle: false);
            if (mapped == IntPtr.Zero)
                throw new InvalidOperationException($"SDL GPU texture transfer-buffer mapping failed for '{debugName}': {SDL_GetError()}");

            fixed (byte* pixels = rgba)
                Buffer.MemoryCopy(pixels, (void*)mapped, rgba.Length, rgba.Length);
            SDL_UnmapGPUTransferBuffer(device, transfer);

            SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(device);
            if (commandBuffer is null)
                throw new InvalidOperationException($"SDL GPU command-buffer acquisition failed for texture '{debugName}': {SDL_GetError()}");

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);
            if (copyPass is null)
                throw new InvalidOperationException($"SDL GPU copy-pass creation failed for texture '{debugName}': {SDL_GetError()}");

            var source = new SDL_GPUTextureTransferInfo
            {
                transfer_buffer = transfer,
                pixels_per_row = width,
                rows_per_layer = height,
            };
            var destination = new SDL_GPUTextureRegion
            {
                texture = texture,
                w = width,
                h = height,
                d = 1,
            };
            SDL_UploadToGPUTexture(copyPass, &source, &destination, cycle: false);
            SDL_EndGPUCopyPass(copyPass);

            if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
                throw new InvalidOperationException($"SDL GPU command-buffer submission failed for texture '{debugName}': {SDL_GetError()}");

            return new UploadedStaticMeshTexture(device, texture);
        }
        catch
        {
            SDL_ReleaseGPUTexture(device, texture);
            throw;
        }
        finally
        {
            SDL_ReleaseGPUTransferBuffer(device, transfer);
        }
    }

    private readonly record struct StaticMeshTexturePixels(byte[] Rgba, uint Width, uint Height);
}
