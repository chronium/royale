using Royale.Client.Platform;

namespace Royale.Client.Tests;

public sealed class SdlApplicationOptionsTests
{
    [Fact]
    public void ParseUsesDefaultsWithoutScreenshotArguments()
    {
        SdlApplicationOptions options = SdlApplicationOptions.Parse([]);

        Assert.Null(options.ScreenshotPath);
        Assert.Equal(0, options.ScreenshotAfterFrames);
    }

    [Fact]
    public void ParseDefaultsScreenshotToFirstFrame()
    {
        SdlApplicationOptions options = SdlApplicationOptions.Parse(["--screenshot", "/tmp/royale.bmp"]);

        Assert.Equal("/tmp/royale.bmp", options.ScreenshotPath);
        Assert.Equal(1, options.ScreenshotAfterFrames);
    }

    [Fact]
    public void ParseAcceptsScreenshotFrameDelay()
    {
        SdlApplicationOptions options = SdlApplicationOptions.Parse(["--screenshot", "/tmp/royale.bmp", "--screenshot-after-frames", "5"]);

        Assert.Equal("/tmp/royale.bmp", options.ScreenshotPath);
        Assert.Equal(5, options.ScreenshotAfterFrames);
    }

    [Fact]
    public void ParseRejectsScreenshotFrameDelayWithoutScreenshotPath()
    {
        Assert.Throws<ArgumentException>(() => SdlApplicationOptions.Parse(["--screenshot-after-frames", "5"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidScreenshotFrameDelay(string value)
    {
        Assert.Throws<ArgumentException>(() => SdlApplicationOptions.Parse(["--screenshot", "/tmp/royale.bmp", "--screenshot-after-frames", value]));
    }
}
