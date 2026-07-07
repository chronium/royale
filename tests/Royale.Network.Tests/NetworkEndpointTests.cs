using Royale.Network;

namespace Royale.Network.Tests;

public sealed class NetworkEndpointTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectsEmptyHost(string host)
    {
        Assert.Throws<ArgumentException>(() => new NetworkEndpoint(host, 7777));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void RejectsInvalidPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkEndpoint("127.0.0.1", port));
    }

    [Fact]
    public void StoresHostAndPort()
    {
        NetworkEndpoint endpoint = new("127.0.0.1", 7777);

        Assert.Equal("127.0.0.1", endpoint.Host);
        Assert.Equal(7777, endpoint.Port);
        Assert.Equal("127.0.0.1:7777", endpoint.ToString());
    }
}
