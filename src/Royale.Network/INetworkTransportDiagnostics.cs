namespace Royale.Network;

public interface INetworkTransportDiagnostics
{
    bool TryGetPeerStatistics(NetworkPeerId peerId, out NetworkPeerStatistics statistics);
}
