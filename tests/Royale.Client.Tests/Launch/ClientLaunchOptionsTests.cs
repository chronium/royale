using System.Numerics;
using Royale.Client.Launch;
using Royale.Client.Presentation;
using Royale.Rendering;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

namespace Royale.Client.Tests.Launch;

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
        Assert.Equal(RenderViewMode.WorldAndDebug, options.RenderViewMode);
        Assert.True(options.TelemetryVisible);
        Assert.Null(options.CameraPosition);
        Assert.Null(options.CameraLookAt);
        Assert.Null(options.ScreenshotPath);
        Assert.Equal(0, options.ScreenshotAfterFrames);
    }

    [Theory]
    [InlineData("normal", RenderViewMode.Normal)]
    [InlineData("world-and-debug", RenderViewMode.WorldAndDebug)]
    [InlineData("debug-only", RenderViewMode.DebugOnly)]
    [InlineData("collision-solids", RenderViewMode.CollisionSolids)]
    public void ParseAcceptsExplicitRenderView(string value, RenderViewMode expected)
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--render-view", value]);

        Assert.Equal(expected, options.RenderViewMode);
    }

    [Fact]
    public void ParseRejectsInvalidRenderView()
    {
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--render-view", "wireframe"]));
    }

    [Fact]
    public void ParseCanHideTelemetryForDeterministicCapture()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--hide-telemetry"]);

        Assert.False(options.TelemetryVisible);
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
    [InlineData("--config")]
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

    [Fact]
    public void CommittedProductionProfileParsesExpectedValues()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(
            ["--config", ConfigurationPath("client.production.json")]);

        Assert.Equal(ClientLaunchMode.Offline, options.Mode);
        Assert.Null(options.ConnectHost);
        Assert.Equal(7777, options.Port);
        Assert.Equal("graybox", options.MapId);
        Assert.Equal(ClientCameraMode.Gameplay, options.CameraMode);
    }

    [Fact]
    public void CommittedDevelopmentProfileParsesExpectedValues()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse(
            ["--config", ConfigurationPath("client.development.json")]);

        Assert.Equal(ClientLaunchMode.Connect, options.Mode);
        Assert.Equal("127.0.0.1", options.ConnectHost);
        Assert.Equal(7777, options.Port);
        Assert.Equal("graybox", options.MapId);
        Assert.Equal(ClientCameraMode.Gameplay, options.CameraMode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CliOverridesProfileRegardlessOfConfigArgumentOrder(bool configFirst)
    {
        string configPath = ConfigurationPath("client.development.json");
        string[] args = configFirst
            ? ["--config", configPath, "--offline", "--port", "7788"]
            : ["--offline", "--port", "7788", "--config", configPath];

        ClientLaunchOptions options = ClientLaunchOptions.Parse(args);

        Assert.Equal(ClientLaunchMode.Offline, options.Mode);
        Assert.Null(options.ConnectHost);
        Assert.Equal(7788, options.Port);
    }

    [Fact]
    public void CliConnectReplacesConfiguredModeAndHost()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Parse([
            "--config",
            ConfigurationPath("client.production.json"),
            "--connect",
            "localhost"
        ]);

        Assert.Equal(ClientLaunchMode.Connect, options.Mode);
        Assert.Equal("localhost", options.ConnectHost);
    }

    [Fact]
    public void ParseAllowsCommentsAndTrailingCommas()
    {
        using var profile = new TemporaryJsonFile("{ /* local */ \"port\": 7788, }");

        Assert.Equal(7788, ClientLaunchOptions.Parse(["--config", profile.Path]).Port);
    }

    [Fact]
    public void ProfileSupportsEveryClientStartupOption()
    {
        using var profile = new TemporaryJsonFile("""
            {
              "mode": "connect",
              "connectHost": "localhost",
              "port": 7788,
              "mapId": "test-map",
              "cameraMode": "freecam",
              "cameraPosition": "4,2.2,3",
              "cameraLookAt": "1.75,0.7,-1.35",
              "screenshotPath": "/tmp/royale-profile.bmp",
              "screenshotAfterFrames": 5
            }
            """);

        ClientLaunchOptions options = ClientLaunchOptions.Parse(["--config", profile.Path]);

        Assert.Equal(ClientLaunchMode.Connect, options.Mode);
        Assert.Equal("localhost", options.ConnectHost);
        Assert.Equal(7788, options.Port);
        Assert.Equal("test-map", options.MapId);
        Assert.Equal(ClientCameraMode.Freecam, options.CameraMode);
        AssertVector(new Vector3(4.0f, 2.2f, 3.0f), Assert.IsType<Vector3>(options.CameraPosition));
        AssertVector(new Vector3(1.75f, 0.7f, -1.35f), Assert.IsType<Vector3>(options.CameraLookAt));
        Assert.Equal("/tmp/royale-profile.bmp", options.ScreenshotPath);
        Assert.Equal(5, options.ScreenshotAfterFrames);
    }

    [Fact]
    public void ConfigPathResolvesRelativeToCurrentWorkingDirectory()
    {
        string relativePath = System.IO.Path.GetRelativePath(
            Environment.CurrentDirectory,
            ConfigurationPath("client.production.json"));

        Assert.Equal(ClientLaunchMode.Offline, ClientLaunchOptions.Parse(["--config", relativePath]).Mode);
    }

    [Fact]
    public void ParseRejectsMissingMalformedUnknownAndDuplicateConfig()
    {
        using var malformed = new TemporaryJsonFile("{ nope }");
        using var unknown = new TemporaryJsonFile("{ \"unknown\": 1 }");

        Assert.Throws<ArgumentException>(
            () => ClientLaunchOptions.Parse(["--config", System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json")]));
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--config", malformed.Path]));
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--config", unknown.Path]));
        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(
            ["--config", malformed.Path, "--config", unknown.Path]));
    }

    [Theory]
    [InlineData("{ \"mode\": \"invalid\" }")]
    [InlineData("{ \"mode\": \"connect\" }")]
    [InlineData("{ \"mode\": \"offline\", \"connectHost\": \"localhost\" }")]
    [InlineData("{ \"port\": 0 }")]
    [InlineData("{ \"cameraMode\": \"gameplay\", \"cameraPosition\": \"1,2,3\" }")]
    [InlineData("{ \"screenshotAfterFrames\": 2 }")]
    [InlineData("{ \"mode\": null }")]
    [InlineData("{ \"port\": null }")]
    public void ParseRejectsInvalidOrConflictingProfileSettings(string json)
    {
        using var profile = new TemporaryJsonFile(json);

        Assert.Throws<ArgumentException>(() => ClientLaunchOptions.Parse(["--config", profile.Path]));
    }

    private static string ConfigurationPath(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = System.IO.Path.Combine(directory.FullName, "config", fileName);
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate committed profile '{fileName}'.");
    }

    private sealed class TemporaryJsonFile : IDisposable
    {
        public TemporaryJsonFile(string json)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"royale-{Guid.NewGuid():N}.json");
            File.WriteAllText(Path, json);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
