using SDL;

namespace Royale.Client.Rendering;

public static class BmpScreenshotWriter
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int BytesPerPixel = 4;

    public static void Save(string path, ReadOnlySpan<byte> pixels, int width, int height, SDL_GPUTextureFormat format)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Screenshot width must be positive.");

        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Screenshot height must be positive.");

        int expectedBytes = checked(width * height * BytesPerPixel);

        if (pixels.Length < expectedBytes)
            throw new ArgumentException("Screenshot pixel buffer is smaller than the image dimensions require.", nameof(pixels));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        int imageBytes = expectedBytes;
        int fileBytes = FileHeaderSize + InfoHeaderSize + imageBytes;
        byte[] bmp = new byte[fileBytes];

        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        WriteInt32(bmp, 2, fileBytes);
        WriteInt32(bmp, 10, FileHeaderSize + InfoHeaderSize);
        WriteInt32(bmp, 14, InfoHeaderSize);
        WriteInt32(bmp, 18, width);
        WriteInt32(bmp, 22, -height);
        WriteInt16(bmp, 26, 1);
        WriteInt16(bmp, 28, 32);
        WriteInt32(bmp, 34, imageBytes);

        Span<byte> destination = bmp.AsSpan(FileHeaderSize + InfoHeaderSize);

        for (int pixelOffset = 0; pixelOffset < expectedBytes; pixelOffset += BytesPerPixel)
        {
            byte first = pixels[pixelOffset];
            byte second = pixels[pixelOffset + 1];
            byte third = pixels[pixelOffset + 2];
            byte alpha = pixels[pixelOffset + 3];

            switch (format)
            {
                case SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM:
                case SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM_SRGB:
                    destination[pixelOffset] = third;
                    destination[pixelOffset + 1] = second;
                    destination[pixelOffset + 2] = first;
                    destination[pixelOffset + 3] = alpha;
                    break;

                case SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM:
                case SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM_SRGB:
                    destination[pixelOffset] = first;
                    destination[pixelOffset + 1] = second;
                    destination[pixelOffset + 2] = third;
                    destination[pixelOffset + 3] = alpha;
                    break;

                default:
                    throw new NotSupportedException($"Unsupported screenshot texture format: {format}");
            }
        }

        File.WriteAllBytes(path, bmp);
    }

    private static void WriteInt16(Span<byte> destination, int offset, short value)
    {
        destination[offset] = (byte)value;
        destination[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteInt32(Span<byte> destination, int offset, int value)
    {
        destination[offset] = (byte)value;
        destination[offset + 1] = (byte)(value >> 8);
        destination[offset + 2] = (byte)(value >> 16);
        destination[offset + 3] = (byte)(value >> 24);
    }
}
