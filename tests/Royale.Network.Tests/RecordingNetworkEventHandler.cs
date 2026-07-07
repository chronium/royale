using System.Net.Sockets;
using Royale.Network;

namespace Royale.Network.Tests;

internal sealed class RecordingNetworkEventHandler : INetworkEventHandler
{
    public List<NetworkPeerId> ConnectedPeers { get; } = [];

    public List<NetworkPeerId> DisconnectedPeers { get; } = [];

    public List<ReceivedPacket> Packets { get; } = [];

    public List<(NetworkEndpoint? Endpoint, SocketError Error)> Errors { get; } = [];

    public List<(NetworkPeerId PeerId, int LatencyMilliseconds)> LatencyUpdates { get; } = [];

    public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
        ConnectedPeers.Add(peerId);
    }

    public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
    {
        DisconnectedPeers.Add(peerId);
    }

    public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        Packets.Add(new ReceivedPacket(peerId, packet.ToArray(), delivery, channel));
    }

    public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
    {
        Errors.Add((endpoint, socketError));
    }

    public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
    {
        LatencyUpdates.Add((peerId, latencyMilliseconds));
    }
}
