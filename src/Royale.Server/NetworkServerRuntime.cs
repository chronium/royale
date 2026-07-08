using System.Net.Sockets;
using System.Diagnostics;
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
    private readonly ServerObservability? observability;
    private readonly Dictionary<NetworkPeerId, InProcessClientConnection> peerConnections = [];
    private bool disposed;

    public NetworkServerRuntime(
        INetworkTransport transport,
        InProcessServerSession session,
        ServerObservability? observability = null)
    {
        this.transport = transport;
        this.session = session;
        this.observability = observability;
        handshakeServer = new NetworkHandshakeServer(transport, AcceptClient, HandshakeRejected);
        inputReceiver = new ServerInputReceiver(handshakeServer.AcceptedPeers, EnqueueInputCommand, InvalidInput);
        snapshotSender = new ServerSnapshotSender(transport, handshakeServer.AcceptedPeers, DrainLatestSnapshot);
        UpdateObservabilityState();
    }

    public ulong CurrentTick => session.CurrentTick;

    public string MapId => session.MapId;

    public int ActivePlayerCount => session.ActivePlayerCount;

    public int ConnectedClientCount => session.ConnectedClientCount;

    public int QueuedInputCommandCount => session.QueuedInputCommandCount;

    public IReadOnlyDictionary<NetworkPeerId, ServerAccept> AcceptedPeers => handshakeServer.AcceptedPeers;

    public IReadOnlyList<ServerPlayerDebugState> GetPlayerDebugStates()
    {
        ThrowIfDisposed();

        return session.GetPlayerDebugStates(CreatePeerIdsByPlayerId());
    }

    public static NetworkServerRuntime Listen(string mapId, int port, ServerObservability? observability = null)
    {
        var transport = new LiteNetLibNetworkTransport();
        transport.Start(port);

        try
        {
            return new NetworkServerRuntime(transport, InProcessServerSession.Create(mapId), observability);
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

        long started = Stopwatch.GetTimestamp();
        int sentSnapshots = 0;
        transport.Poll(this);
        session.Step();
        sentSnapshots = snapshotSender.SendDueSnapshots(session.CurrentTick);
        observability?.SnapshotsSent(sentSnapshots);
        UpdateObservabilityState();
        observability?.PlayerDebugStates(GetPlayerDebugStates());
        observability?.TickCompleted(Stopwatch.GetElapsedTime(started));
        return sentSnapshots;
    }

    public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
        observability?.PeerConnected(peerId, endpoint);
        handshakeServer.Connected(peerId, endpoint);
        inputReceiver.Connected(peerId, endpoint);
    }

    public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
    {
        bool hadConnection = peerConnections.TryGetValue(peerId, out InProcessClientConnection existingConnection);
        observability?.PeerDisconnected(peerId, reason, hadConnection ? existingConnection : null);
        inputReceiver.Disconnected(peerId, reason);
        handshakeServer.Disconnected(peerId, reason);

        if (peerConnections.Remove(peerId, out InProcessClientConnection connection))
        {
            session.DisconnectClient(connection);
            observability?.ClientDisconnected(reason);
        }

        UpdateObservabilityState();
    }

    public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        observability?.PacketReceived(TryReadMessageType(packet.Span), delivery, channel);
        handshakeServer.PacketReceived(peerId, packet, delivery, channel);
        inputReceiver.PacketReceived(peerId, packet, delivery, channel);
        UpdateObservabilityState();
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
        observability?.ClientAccepted(peerId, connection);
        UpdateObservabilityState();
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

        if (!session.TryEnqueueInputCommand(connection, command))
            observability?.InvalidCommand(peerId, connection);

        UpdateObservabilityState();
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

    private void HandshakeRejected(NetworkPeerId peerId, ServerRejectReason reason, string detail)
    {
        observability?.HandshakeRejected(peerId, reason, detail);
    }

    private void InvalidInput(NetworkPeerId peerId, ServerInputRejectReason reason)
    {
        observability?.InvalidInput(peerId, reason);
    }

    private void UpdateObservabilityState()
    {
        observability?.UpdateState(
            session.ConnectedClientCount,
            session.ActivePlayerCount,
            session.LivingPlayerCount,
            session.MatchPhase,
            session.QueuedInputCommandCount);
    }

    private Dictionary<ServerPlayerId, int> CreatePeerIdsByPlayerId() =>
        peerConnections.ToDictionary(
            pair => pair.Value.PlayerId,
            pair => pair.Key.Value);

    private static ProtocolMessageType? TryReadMessageType(ReadOnlySpan<byte> packet)
    {
        return ProtocolPacketFramer.TryReadPacket(
            packet,
            out ProtocolPacketHeader header,
            out _,
            out _)
            ? header.MessageType
            : null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
