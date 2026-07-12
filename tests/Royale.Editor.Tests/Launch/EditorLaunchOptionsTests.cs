using Royale.Editor.Launch;

namespace Royale.Editor.Tests.Launch;

public sealed class EditorLaunchOptionsTests
{
    [Fact]
    public void DefaultUsesGraybox()
    {
        EditorLaunchOptions value = EditorLaunchOptions.Parse([]);
        Assert.Equal("graybox", value.MapId);
        Assert.Null(value.MapFilePath);
        Assert.Null(value.ScreenshotPath);
        Assert.False(value.ResetLayout);
    }

    [Fact]
    public void ParsesAllOptions()
    {
        EditorLaunchOptions value = EditorLaunchOptions.Parse([
            "--map",
            "prototype-arena",
            "--screenshot",
            "/tmp/editor.bmp",
            "--screenshot-after-frames",
            "4",
            "--reset-layout",
        ]);
        Assert.Equal("prototype-arena", value.MapId);
        Assert.Equal(4, value.ScreenshotAfterFrames);
        Assert.True(value.ResetLayout);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void RejectsInvalidFrameCounts(string count) =>
        Assert.Throws<ArgumentException>(() =>
            EditorLaunchOptions.Parse(["--screenshot", "x", "--screenshot-after-frames", count]));

    [Fact]
    public void FrameCountRequiresScreenshot() =>
        Assert.Throws<ArgumentException>(() =>
            EditorLaunchOptions.Parse(["--screenshot-after-frames", "2"]));

    [Fact]
    public void ParsesExplicitMapFile()
    {
        EditorLaunchOptions value = EditorLaunchOptions.Parse(["--map-file", "/tmp/custom.json"]);
        Assert.Equal("/tmp/custom.json", value.MapFilePath);
    }
}
