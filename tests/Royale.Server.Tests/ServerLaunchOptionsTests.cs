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
