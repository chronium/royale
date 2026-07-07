using Royale.Network;

namespace Royale.Network.Tests;

public sealed class LiteNetLibNetworkTransportLifecycleTests
{
    [Fact]
    public void StartRejectsInvalidPort()
    {
        using LiteNetLibNetworkTransport transport = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => transport.Start(-1));
    }

    [Fact]
    public void StartRejectsSecondStart()
    {
        using LiteNetLibNetworkTransport transport = new();
        transport.Start(0);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => transport.Start(0));
        Assert.Contains("already been started", exception.Message);
    }

    [Fact]
    public void OperationsAfterDisposeThrow()
    {
        LiteNetLibNetworkTransport transport = new();
        transport.Start(0);
        transport.Dispose();

        Assert.Throws<ObjectDisposedException>(() => transport.Start(0));
        Assert.Throws<ObjectDisposedException>(() => transport.Connect(new NetworkEndpoint("127.0.0.1", 7777)));
        Assert.Throws<ObjectDisposedException>(() => transport.Send(new NetworkPeerId(0), [1], NetworkDelivery.Unreliable));
        Assert.Throws<ObjectDisposedException>(() => transport.Disconnect(new NetworkPeerId(0)));
        Assert.Throws<ObjectDisposedException>(() => transport.Poll(new RecordingNetworkEventHandler()));
    }

    [Fact]
    public void OperationsBeforeStartThrow()
    {
        using LiteNetLibNetworkTransport transport = new();

        Assert.Throws<InvalidOperationException>(() => transport.Connect(new NetworkEndpoint("127.0.0.1", 7777)));
        Assert.Throws<InvalidOperationException>(() => transport.Send(new NetworkPeerId(0), [1], NetworkDelivery.Unreliable));
        Assert.Throws<InvalidOperationException>(() => transport.Disconnect(new NetworkPeerId(0)));
        Assert.Throws<InvalidOperationException>(() => transport.Poll(new RecordingNetworkEventHandler()));
    }

    [Fact]
    public void SendRejectsUnsupportedChannel()
    {
        using LiteNetLibNetworkTransport transport = new();
        transport.Start(0);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => transport.Send(new NetworkPeerId(0), [1], NetworkDelivery.Unreliable, channel: 64));

        Assert.Equal("channel", exception.ParamName);
    }
}
