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
        Assert.Null(value.ProjectPath);
        Assert.Null(value.ScreenshotPath);
        Assert.False(value.ResetLayout);
        Assert.False(value.McpEnabled);
        Assert.Equal(EditorLaunchOptions.DefaultMcpPort, value.McpPort);
    }

    [Fact]
    public void ParsesAllOptions()
    {
        EditorLaunchOptions value = EditorLaunchOptions.Parse([
            "--map",
            "prototype-arena",
            "--screenshot",
            "/tmp/editor.png",
            "--screenshot-after-frames",
            "4",
            "--reset-layout",
            "--mcp",
            "--mcp-port",
            "52000",
        ]);
        Assert.Equal("prototype-arena", value.MapId);
        Assert.Equal(4, value.ScreenshotAfterFrames);
        Assert.True(value.ResetLayout);
        Assert.True(value.McpEnabled);
        Assert.Equal(52000, value.McpPort);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void RejectsInvalidFrameCounts(string count) =>
        Assert.Throws<ArgumentException>(() =>
            EditorLaunchOptions.Parse(["--screenshot", "x.png", "--screenshot-after-frames", count]));

    [Fact]
    public void FrameCountRequiresScreenshot() =>
        Assert.Throws<ArgumentException>(() =>
            EditorLaunchOptions.Parse(["--screenshot-after-frames", "2"]));

    [Theory]
    [InlineData("editor.bmp")]
    [InlineData("editor.jpg")]
    [InlineData("editor")]
    public void RejectsNonPngScreenshotPaths(string path) =>
        Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(["--screenshot", path]));

    [Fact]
    public void ParsesExplicitMapFile()
    {
        EditorLaunchOptions value = EditorLaunchOptions.Parse(["--map-file", "/tmp/custom.json"]);
        Assert.Equal("/tmp/custom.json", value.MapFilePath);
    }

    [Fact]
    public void ParsesExplicitProject() =>
        Assert.Equal("/tmp/arena.royaleproject", EditorLaunchOptions.Parse(["--project", "/tmp/arena.royaleproject"]).ProjectPath);

    [Theory]
    [InlineData("--map")]
    [InlineData("--map-file")]
    public void RejectsProjectCombinedWithMapTarget(string option) =>
        Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(["--project", "p", option, "graybox"]));

    [Fact]
    public void McpUsesDefaultPort()
    {
        EditorLaunchOptions value = EditorLaunchOptions.Parse(["--mcp"]);

        Assert.True(value.McpEnabled);
        Assert.Equal(EditorLaunchOptions.DefaultMcpPort, value.McpPort);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void RejectsInvalidMcpPorts(string port) =>
        Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(["--mcp", "--mcp-port", port]));

    [Fact]
    public void McpPortRequiresMcp() =>
        Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(["--mcp-port", "52000"]));
}
