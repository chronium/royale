namespace Royale.Rendering.Screenshots;

public static class PngScreenshotWriter
{
    public static void Save(string path, ReadOnlySpan<byte> rgba, int width, int height)
    {
        ScreenshotPathValidator.Validate(path, "screenshot path");
        PngImageCodec.Write(path, rgba, width, height);
    }
}

public static class ScreenshotPathValidator
{
    public static void Validate(string path, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException($"{argumentName} must be a non-empty .png path.");
        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{argumentName} must end in .png.");
    }
}
