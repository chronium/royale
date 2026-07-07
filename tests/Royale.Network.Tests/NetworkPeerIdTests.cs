using Royale.Network;

namespace Royale.Network.Tests;

public sealed class NetworkPeerIdTests
{
    [Fact]
    public void RejectsNegativeValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkPeerId(-1));
    }

    [Fact]
    public void StoresOpaqueValue()
    {
        NetworkPeerId peerId = new(42);

        Assert.Equal(42, peerId.Value);
        Assert.Equal("42", peerId.ToString());
    }
}
