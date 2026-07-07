using System.Net.Sockets;
using Royale.Network;
using Royale.Protocol;

namespace Royale.Server;

public sealed class NetworkServerRuntime : INetworkEventHandler, IDisposable
{
    private readonly INetworkTransport transport;
    private readonly InProcessServerSession session;
    private readonly NetworkHandshakeServer handshakeServer;
    private readonly ServerInputReceiver inputReceiver;
    private readonly ServerSnapshotSender snapshotSender;
    private readonly Dictionary<NetworkPeerId, InProcessClientConnection> peerConnections = [];
    private bool disposed;

    public NetworkServerRuntime(INetworkTransport transport, InProcessServerSession session)
    {
        this.transport = transport;
        this.session = session;
        handshakeServer = new NetworkHandshakeServer(transport, AcceptClient);
        inputReceiver = new ServerInputReceiver(handshakeServer.AcceptedPeers, EnqueueInputCommand);
        snapshotSender = new ServerSnapshotSender(transport, handshakeServer.AcceptedPeers, DrainLatestSnapshot);
    }

    public ulong CurrentTick => session.CurrentTick;

    public string MapId => session.MapId;

    public int ActivePlayerCount => session.ActivePlayerCount;

    public int ConnectedClientCount => session.ConnectedClientCount;

    public IReadOnlyDictionary<NetworkPeerId, ServerAccept> AcceptedPeers => handshakeServer.AcceptedPeers;

    public static NetworkServerRuntime Listen(string mapId, int port)
    {
        var transport = new LiteNetLibNetworkTransport();
        transport.Start(port);

        try
        {
            return new NetworkServerRuntime(transport, InProcessServerSession.Create(mapId));
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    public int Step()
    {
        ThrowIfDisposed();

        transport.Poll(this);
        session.Step();
        return snapshotSender.SendDueSnapshots(session.CurrentTick);
    }

    public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
        handshakeServer.Connected(peerId, endpoint);
        inputReceiver.Connected(peerId, endpoint);
    }

    public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
    {
        inputReceiver.Disconnected(peerId, reason);
        handshakeServer.Disconnected(peerId, reason);

        if (peerConnections.Remove(peerId, out InProcessClientConnection connection))
            session.DisconnectClient(connection);
    }

    public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        handshakeServer.PacketReceived(peerId, packet, delivery, channel);
        inputReceiver.PacketReceived(peerId, packet, delivery, channel);
    }

    public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
    {
        handshakeServer.NetworkError(endpoint, socketError);
        inputReceiver.NetworkError(endpoint, socketError);
    }

    public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
    {
        handshakeServer.LatencyUpdated(peerId, latencyMilliseconds);
        inputReceiver.LatencyUpdated(peerId, latencyMilliseconds);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        session.Dispose();
        transport.Dispose();
        peerConnections.Clear();
        disposed = true;
    }

    private NetworkHandshakeAcceptResult AcceptClient(NetworkPeerId peerId)
    {
        InProcessClientConnection connection = session.ConnectClient();
        peerConnections.Add(peerId, connection);
        return new NetworkHandshakeAcceptResult(
            connection.ConnectionId.Value,
            connection.PlayerId.Value,
            session.CurrentTick,
            session.MapId);
    }

    private void EnqueueInputCommand(NetworkPeerId peerId, ServerAccept accept, PlayerInputCommand command)
    {
        if (!peerConnections.TryGetValue(peerId, out InProcessClientConnection connection) ||
            connection.ConnectionId.Value != accept.ConnectionId ||
            connection.PlayerId.Value != accept.PlayerId)
        {
            return;
        }

        session.TryEnqueueInputCommand(connection, command);
    }

    private ServerSnapshot? DrainLatestSnapshot(NetworkPeerId peerId, ServerAccept accept)
    {
        if (!peerConnections.TryGetValue(peerId, out InProcessClientConnection connection) ||
            connection.ConnectionId.Value != accept.ConnectionId ||
            connection.PlayerId.Value != accept.PlayerId)
        {
            return null;
        }

        IReadOnlyList<ServerSnapshot> snapshots = session.DrainSnapshots(connection);
        return snapshots.Count == 0 ? null : snapshots[^1];
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
