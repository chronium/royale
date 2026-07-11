using System.Net.Sockets;

namespace Royale.Network.Transport;

public interface INetworkEventHandler
{
    void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint);

    void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason);

    void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel);

    void NetworkError(NetworkEndpoint? endpoint, SocketError socketError);

    void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds);
}
