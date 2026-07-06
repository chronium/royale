using System.Numerics;
using Royale.Client.Launch;
using Royale.Client.Presentation;
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
        Assert.Equal(ClientCameraMode.Gameplay, options.CameraMode);
        Assert.Null(options.CameraPosition);
        Assert.Null(options.CameraLookAt);
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
    public void ParseAcceptsFreecamCameraMode()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--camera-mode", "freecam"]);

        Assert.Equal(ClientCameraMode.Freecam, options.CameraMode);
        Assert.Null(options.CameraPosition);
        Assert.Null(options.CameraLookAt);
    }

    [Fact]
    public void ParseAcceptsFreecamPositionAndLookAt()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse([
            "--camera-mode",
            "freecam",
            "--camera-position",
            "4,2.2,3",
            "--camera-look-at",
            "1.75,0.7,-1.35"
        ]);

        Assert.Equal(ClientCameraMode.Freecam, options.CameraMode);
        AssertVector(new Vector3(4.0f, 2.2f, 3.0f), Assert.IsType<Vector3>(options.CameraPosition));
        AssertVector(new Vector3(1.75f, 0.7f, -1.35f), Assert.IsType<Vector3>(options.CameraLookAt));
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
        Assert.Equal(ClientCameraMode.Gameplay, options.CameraMode);
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
    [InlineData("debug")]
    [InlineData("Freecam")]
    [InlineData("free")]
    [InlineData("0")]
    public void ParseRejectsUnknownCameraModes(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--camera-mode", value]));
    }

    [Theory]
    [InlineData("--camera-position")]
    [InlineData("--camera-look-at")]
    public void ParseRejectsCameraVectorsWithoutFreecamMode(string option)
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse([option, "1,2,3"]));
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--camera-mode", "gameplay", option, "1,2,3"]));
    }

    [Theory]
    [InlineData("1,2")]
    [InlineData("1,2,3,4")]
    [InlineData("1,,3")]
    [InlineData("1;2;3")]
    [InlineData("NaN,2,3")]
    [InlineData("Infinity,2,3")]
    [InlineData("-Infinity,2,3")]
    public void ParseRejectsMalformedCameraVectors(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse([
            "--camera-mode",
            "freecam",
            "--camera-position",
            value
        ]));

        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse([
            "--camera-mode",
            "freecam",
            "--camera-look-at",
            value
        ]));
    }

    [Fact]
    public void ParseRejectsCameraLookAtEqualToCameraPosition()
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse([
            "--camera-mode",
            "freecam",
            "--camera-position",
            "1,2,3",
            "--camera-look-at",
            "1,2,3"
        ]));
    }

    [Theory]
    [InlineData("--connect")]
    [InlineData("--port")]
    [InlineData("--map")]
    [InlineData("--camera-mode")]
    [InlineData("--camera-position")]
    [InlineData("--camera-look-at")]
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

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
