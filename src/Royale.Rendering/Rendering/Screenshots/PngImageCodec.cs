using StbImageSharp;
using StbImageWriteSharp;

namespace Royale.Rendering.Screenshots;

public static class PngImageCodec
{
    private const int BytesPerPixel = 4;
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height)
    {
        ValidatePixels(rgba.Length, width, height);
        using var output = new MemoryStream();
        new ImageWriter().WritePng(rgba.ToArray(), width, height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, output);
        return output.ToArray();
    }

    public static PngImage Decode(ReadOnlySpan<byte> encoded)
    {
        if (!encoded.StartsWith(Signature))
            throw new InvalidDataException("Data does not have a PNG signature.");

        try
        {
            ImageResult image = ImageResult.FromMemory(encoded.ToArray(), StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            return new PngImage(image.Width, image.Height, image.Data);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException("PNG data is malformed or unsupported.", exception);
        }
    }

    public static void Write(string path, ReadOnlySpan<byte> rgba, int width, int height, bool atomic = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] encoded = Encode(rgba, width, height);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (!atomic)
        {
            File.WriteAllBytes(fullPath, encoded);
            return;
        }

        string temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporaryPath, encoded);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void ValidatePixels(int length, int width, int height)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be positive.");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be positive.");

        int expected = checked(width * height * BytesPerPixel);
        if (length != expected)
            throw new ArgumentException($"RGBA pixel buffer must contain exactly {expected} bytes.", nameof(length));
    }
}

public sealed record PngImage(int Width, int Height, byte[] RgbaBytes);
