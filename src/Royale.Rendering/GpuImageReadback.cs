using SDL;

namespace Royale.Rendering;

public sealed record GpuImageReadback(int Width, int Height, byte[] RgbaBytes)
{
    public static byte[] NormalizeToRgba(ReadOnlySpan<byte> pixels, SDL_GPUTextureFormat format)
    {
        byte[] normalized = pixels.ToArray();
        if (format is SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM or
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM_SRGB)
        {
            for (int offset = 0; offset < normalized.Length; offset += 4)
                (normalized[offset], normalized[offset + 2]) = (normalized[offset + 2], normalized[offset]);
        }
        else if (format is not (SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM or
                 SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM_SRGB))
        {
            throw new NotSupportedException($"GPU readback format {format} is not an RGBA or BGRA 8-bit format.");
        }

        return normalized;
    }
}
