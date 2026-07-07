namespace Royale.Network;

public interface INetworkTransport : IDisposable
{
    void Start(int port);

    NetworkPeerId Connect(NetworkEndpoint endpoint);

    void Send(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel = 0);

    void Disconnect(NetworkPeerId peerId);

    void Poll(INetworkEventHandler handler);
}
