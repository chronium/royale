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
}
