using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;

namespace Royale.Network.Tests.Transport;

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
