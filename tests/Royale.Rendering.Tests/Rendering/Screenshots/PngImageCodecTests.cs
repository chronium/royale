using Royale.Rendering.Screenshots;

namespace Royale.Rendering.Tests.Rendering.Screenshots;

public sealed class PngImageCodecTests
{
    [Fact]
    public void EncodeWritesPngSignatureAndRoundTripsRgba()
    {
        byte[] rgba = [0x11, 0x22, 0x33, 0x44, 0xAA, 0xBB, 0xCC, 0xDD];

        byte[] encoded = PngImageCodec.Encode(rgba, 2, 1);
        PngImage decoded = PngImageCodec.Decode(encoded);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, encoded[..8]);
        Assert.Equal(2, decoded.Width);
        Assert.Equal(1, decoded.Height);
        Assert.Equal(rgba, decoded.RgbaBytes);
    }

    [Fact]
    public void EncodeIsDeterministic()
    {
        byte[] rgba = [1, 2, 3, 4, 5, 6, 7, 8];
        Assert.Equal(PngImageCodec.Encode(rgba, 2, 1), PngImageCodec.Encode(rgba, 2, 1));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, 3)]
    [InlineData(1, 1, 5)]
    public void EncodeRejectsInvalidDimensionsOrPixelLength(int width, int height, int length)
    {
        Assert.ThrowsAny<ArgumentException>(() => PngImageCodec.Encode(new byte[length], width, height));
    }

    [Fact]
    public void DecodeRejectsMalformedInput() =>
        Assert.Throws<InvalidDataException>(() => PngImageCodec.Decode([1, 2, 3, 4]));

    [Fact]
    public void AtomicWriteReplacesDestinationWithoutTemporaryFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "image.png");
        try
        {
            PngImageCodec.Write(path, [1, 2, 3, 4], 1, 1, atomic: true);
            PngImageCodec.Write(path, [5, 6, 7, 8], 1, 1, atomic: true);

            Assert.Equal(new byte[] { 5, 6, 7, 8 }, PngImageCodec.Decode(File.ReadAllBytes(path)).RgbaBytes);
            Assert.Single(Directory.EnumerateFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("capture.bmp")]
    [InlineData("capture.jpg")]
    [InlineData("capture")]
    public void ScreenshotWriterRejectsNonPngPaths(string path) =>
        Assert.Throws<ArgumentException>(() => PngScreenshotWriter.Save(path, [1, 2, 3, 4], 1, 1));
}
