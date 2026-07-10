using Royale.Content;
using Royale.Protocol;
using Royale.Server;

namespace Royale.Server.Tests;

public sealed class ServerLaunchOptionsTests
{
    [Fact]
    public void ParseUsesDefaultsWithoutArguments()
    {
        ServerLaunchOptions options = ServerLaunchOptions.Parse([]);

        Assert.Equal(ProtocolConstants.DefaultPort, options.Port);
        Assert.Equal(ContentCatalog.DefaultMapId, options.MapId);
        Assert.Null(options.RunTicks);
        Assert.Equal(MatchStartSettings.DefaultMinimumPlayers, options.MinimumPlayers);
        Assert.Equal(MatchStartSettings.DefaultTargetPlayers, options.TargetPlayers);
        Assert.Equal(MatchStartSettings.DefaultWaitingSeconds, options.WaitingSeconds);
        Assert.Equal(MatchStartSettings.DefaultPreparationSeconds, options.PreparationSeconds);
    }

    [Fact]
    public void ParseAcceptsCustomPortAndMap()
    {
        ServerLaunchOptions options = ServerLaunchOptions.Parse(["--port", "7778", "--map", "test-map"]);

        Assert.Equal(7778, options.Port);
        Assert.Equal("test-map", options.MapId);
        Assert.Null(options.RunTicks);
    }

    [Fact]
    public void ParseAcceptsRunTicks()
    {
        ServerLaunchOptions options = ServerLaunchOptions.Parse(["--run-ticks", "5"]);

        Assert.Equal(5, options.RunTicks);
    }

    [Fact]
    public void ParseAcceptsCustomMinimumPlayers()
    {
        ServerLaunchOptions options = ServerLaunchOptions.Parse(
            ["--minimum-players", "17", "--target-players", "20"]);

        Assert.Equal(17, options.MinimumPlayers);
        Assert.Equal(20, options.TargetPlayers);
    }

    [Fact]
    public void ParseAcceptsLobbyTimingOptions()
    {
        ServerLaunchOptions options = ServerLaunchOptions.Parse(
            ["--target-players", "10", "--waiting-seconds", "5", "--preparation-seconds", "7"]);

        Assert.Equal(10, options.TargetPlayers);
        Assert.Equal(5, options.WaitingSeconds);
        Assert.Equal(7, options.PreparationSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("129")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidMinimumPlayers(string value)
    {
        Assert.Throws<ArgumentException>(
            () => ServerLaunchOptions.Parse(["--minimum-players", value]));
    }

    [Fact]
    public void ParseRejectsMinimumAboveTarget()
    {
        Assert.Throws<ArgumentException>(
            () => ServerLaunchOptions.Parse(["--minimum-players", "9", "--target-players", "8"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("129")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidTargetPlayers(string value)
    {
        Assert.Throws<ArgumentException>(
            () => ServerLaunchOptions.Parse(["--target-players", value]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("35791395")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidOrOverflowingDurations(string value)
    {
        Assert.Throws<ArgumentException>(
            () => ServerLaunchOptions.Parse(["--waiting-seconds", value]));
        Assert.Throws<ArgumentException>(
            () => ServerLaunchOptions.Parse(["--preparation-seconds", value]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidPorts(string value)
    {
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse(["--port", value]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void ParseRejectsInvalidRunTicks(string value)
    {
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse(["--run-ticks", value]));
    }

    [Theory]
    [InlineData("--port")]
    [InlineData("--map")]
    [InlineData("--run-ticks")]
    [InlineData("--minimum-players")]
    [InlineData("--target-players")]
    [InlineData("--waiting-seconds")]
    [InlineData("--preparation-seconds")]
    [InlineData("--config")]
    public void ParseRejectsMissingValues(string option)
    {
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse([option]));
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse([option, "--map"]));
    }

    [Fact]
    public void ParseRejectsUnknownArguments()
    {
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse(["--connect", "localhost"]));
    }

    [Theory]
    [InlineData("server.production.json", 300, 120)]
    [InlineData("server.development.json", 60, 5)]
    public void CommittedServerProfilesParseExpectedValues(
        string fileName,
        int waitingSeconds,
        int preparationSeconds)
    {
        ServerLaunchOptions options = ServerLaunchOptions.Parse(
            ["--config", ConfigurationPath(fileName)]);

        Assert.Equal(7777, options.Port);
        Assert.Equal("graybox", options.MapId);
        Assert.Null(options.RunTicks);
        Assert.Equal(2, options.MinimumPlayers);
        Assert.Equal(8, options.TargetPlayers);
        Assert.Equal(waitingSeconds, options.WaitingSeconds);
        Assert.Equal(preparationSeconds, options.PreparationSeconds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CliOverridesProfileRegardlessOfConfigArgumentOrder(bool configFirst)
    {
        string configPath = ConfigurationPath("server.production.json");
        string[] args = configFirst
            ? ["--config", configPath, "--port", "7788", "--waiting-seconds", "9"]
            : ["--port", "7788", "--waiting-seconds", "9", "--config", configPath];

        ServerLaunchOptions options = ServerLaunchOptions.Parse(args);

        Assert.Equal(7788, options.Port);
        Assert.Equal(9, options.WaitingSeconds);
        Assert.Equal(120, options.PreparationSeconds);
    }

    [Fact]
    public void ParseAllowsCommentsAndTrailingCommas()
    {
        using var profile = new TemporaryJsonFile("{ /* local */ \"port\": 7788, }");

        Assert.Equal(7788, ServerLaunchOptions.Parse(["--config", profile.Path]).Port);
    }

    [Fact]
    public void ProfileSupportsEveryServerStartupOption()
    {
        using var profile = new TemporaryJsonFile("""
            {
              "port": 7788,
              "mapId": "test-map",
              "runTicks": 9,
              "minimumPlayers": 3,
              "targetPlayers": 10,
              "waitingSeconds": 11,
              "preparationSeconds": 12
            }
            """);

        ServerLaunchOptions options = ServerLaunchOptions.Parse(["--config", profile.Path]);

        Assert.Equal(7788, options.Port);
        Assert.Equal("test-map", options.MapId);
        Assert.Equal(9, options.RunTicks);
        Assert.Equal(3, options.MinimumPlayers);
        Assert.Equal(10, options.TargetPlayers);
        Assert.Equal(11, options.WaitingSeconds);
        Assert.Equal(12, options.PreparationSeconds);
    }

    [Fact]
    public void ConfigPathResolvesRelativeToCurrentWorkingDirectory()
    {
        string relativePath = System.IO.Path.GetRelativePath(
            Environment.CurrentDirectory,
            ConfigurationPath("server.production.json"));

        Assert.Equal(8, ServerLaunchOptions.Parse(["--config", relativePath]).TargetPlayers);
    }

    [Fact]
    public void ParseRejectsMissingMalformedUnknownAndDuplicateConfig()
    {
        using var malformed = new TemporaryJsonFile("{ nope }");
        using var unknown = new TemporaryJsonFile("{ \"unknown\": 1 }");

        Assert.Throws<ArgumentException>(
            () => ServerLaunchOptions.Parse(["--config", System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json")]));
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse(["--config", malformed.Path]));
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse(["--config", unknown.Path]));
        Assert.Throws<ArgumentException>(() => ServerLaunchOptions.Parse(
            ["--config", malformed.Path, "--config", unknown.Path]));
    }

    [Theory]
    [InlineData("{ \"minimumPlayers\": 9, \"targetPlayers\": 8 }")]
    [InlineData("{ \"targetPlayers\": 129 }")]
    [InlineData("{ \"waitingSeconds\": 35791395 }")]
    [InlineData("{ \"preparationSeconds\": 0 }")]
    [InlineData("{ \"runTicks\": 0 }")]
    [InlineData("{ \"port\": null }")]
    [InlineData("{ \"mapId\": null }")]
    public void ParseRejectsInvalidProfileCountsAndDurations(string json)
    {
        using var profile = new TemporaryJsonFile(json);

        Assert.ThrowsAny<ArgumentException>(() => ServerLaunchOptions.Parse(["--config", profile.Path]));
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
}
