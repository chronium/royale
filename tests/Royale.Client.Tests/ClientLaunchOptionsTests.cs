using Royale.Client.Platform;
using Royale.Content;
using Royale.Protocol;

namespace Royale.Client.Tests;

public sealed class ClientLaunchOptionsTests
{
    [Fact]
    public void ParseUsesDefaultsWithoutArguments()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse([]);

        Assert.Equal(ClientLaunchMode.Offline, options.Mode);
        Assert.Null(options.ConnectHost);
        Assert.Equal(ProtocolConstants.DefaultPort, options.Port);
        Assert.Equal(ContentCatalog.DefaultMapId, options.MapId);
        Assert.Null(options.ScreenshotPath);
        Assert.Equal(0, options.ScreenshotAfterFrames);
    }

    [Fact]
    public void ParseAcceptsExplicitOfflineMode()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--offline"]);

        Assert.Equal(ClientLaunchMode.Offline, options.Mode);
        Assert.Null(options.ConnectHost);
    }

    [Fact]
    public void ParseAcceptsConnectMode()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--connect", "127.0.0.1"]);

        Assert.Equal(ClientLaunchMode.Connect, options.Mode);
        Assert.Equal("127.0.0.1", options.ConnectHost);
        Assert.Equal(ProtocolConstants.DefaultPort, options.Port);
    }

    [Fact]
    public void ParseAcceptsCustomPort()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--port", "7778"]);

        Assert.Equal(7778, options.Port);
    }

    [Fact]
    public void ParseAcceptsCustomMap()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--map", "test-map"]);

        Assert.Equal("test-map", options.MapId);
    }

    [Fact]
    public void ParseKeepsScreenshotCompatibilityWithLaunchOptions()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse([
            "--connect",
            "localhost",
            "--port",
            "7778",
            "--map",
            "graybox",
            "--screenshot",
            "/tmp/royale.bmp",
            "--screenshot-after-frames",
            "5"
        ]);

        Assert.Equal(ClientLaunchMode.Connect, options.Mode);
        Assert.Equal("localhost", options.ConnectHost);
        Assert.Equal(7778, options.Port);
        Assert.Equal("graybox", options.MapId);
        Assert.Equal("/tmp/royale.bmp", options.ScreenshotPath);
        Assert.Equal(5, options.ScreenshotAfterFrames);
    }

    [Fact]
    public void ParseDefaultsScreenshotToFirstFrame()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--screenshot", "/tmp/royale.bmp"]);

        Assert.Equal("/tmp/royale.bmp", options.ScreenshotPath);
        Assert.Equal(1, options.ScreenshotAfterFrames);
    }

    [Fact]
    public void ParseRejectsScreenshotFrameDelayWithoutScreenshotPath()
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--screenshot-after-frames", "5"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidPorts(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--port", value]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidScreenshotFrameDelay(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--screenshot", "/tmp/royale.bmp", "--screenshot-after-frames", value]));
    }

    [Theory]
    [InlineData("--connect")]
    [InlineData("--port")]
    [InlineData("--map")]
    [InlineData("--screenshot")]
    [InlineData("--screenshot-after-frames")]
    public void ParseRejectsMissingValues(string option)
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse([option]));
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse([option, "--map"]));
    }

    [Fact]
    public void ParseRejectsUnknownArguments()
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--windowed"]));
    }

    [Theory]
    [InlineData("--offline", "--connect")]
    [InlineData("--connect", "localhost", "--offline")]
    public void ParseRejectsConflictingClientModes(params string[] args)
    {
        string[] launchArgs = args.Length == 2
            ? [args[0], args[1], "localhost"]
            : args;

        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(launchArgs));
    }
}
