using System.Net.Sockets;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

using Royale.Network.Transport;

namespace Royale.Network.Handshake;

public sealed class NetworkHandshakeClient : INetworkEventHandler
{
    private readonly INetworkTransport transport;
    private readonly NetworkPeerId serverPeerId;
    private readonly string buildId;
    private readonly string contentVersion;
    private uint nextSequence = 1;

    public NetworkHandshakeClient(
        INetworkTransport transport,
        NetworkPeerId serverPeerId,
        string buildId = ProtocolConstants.BuildId,
        string contentVersion = ProtocolConstants.ContentVersion)
    {
        this.transport = transport;
        this.serverPeerId = serverPeerId;
        this.buildId = buildId;
        this.contentVersion = contentVersion;

        State = NetworkHandshakeClientState.Pending;
        SendClientHello();
    }

    public NetworkHandshakeClientState State { get; private set; }

    public ServerAccept? AcceptedSession { get; private set; }

    public ServerReject? Rejection { get; private set; }

    public void SendClientHello()
    {
        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxClientHelloPayloadSize];
        if (!HandshakePayloadSerializer.TryWriteClientHello(
            new ClientHello(buildId, contentVersion),
            payload,
            out int payloadBytesWritten))
        {
            throw new InvalidOperationException("Client hello payload could not be serialized.");
        }

        SendPacket(ProtocolMessageType.ClientHello, sessionId: 0, payload[..payloadBytesWritten]);
    }

    public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
    }

    public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
    {
        if (peerId == serverPeerId)
            State = NetworkHandshakeClientState.Disconnected;
    }

    public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        if (peerId != serverPeerId || State != NetworkHandshakeClientState.Pending)
            return;

        if (!ProtocolPacketFramer.TryReadPacket(
            packet.Span,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error))
        {
            RejectLocally(ServerRejectReason.MalformedPacket, $"Server handshake frame was invalid: {error}.");
            return;
        }

        switch (header.MessageType)
        {
            case ProtocolMessageType.ServerAccept:
                HandleServerAccept(header, payload);
                break;
            case ProtocolMessageType.ServerReject:
                HandleServerReject(header, payload);
                break;
            default:
                RejectLocally(ServerRejectReason.UnexpectedMessageType, $"Unexpected server handshake message '{header.MessageType}'.");
                break;
        }
    }

    public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
    {
    }

    public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
    {
    }

    private void HandleServerAccept(ProtocolPacketHeader header, ReadOnlySpan<byte> payload)
    {
        if (!HandshakePayloadSerializer.TryReadServerAccept(payload, out ServerAccept? accept) ||
            accept is null ||
            accept.SessionId == 0 ||
            header.SessionId != accept.SessionId)
        {
            RejectLocally(ServerRejectReason.MalformedPacket, "Server accept payload was invalid.");
            return;
        }

        AcceptedSession = accept;
        Rejection = null;
        State = NetworkHandshakeClientState.Accepted;
    }

    private void HandleServerReject(ProtocolPacketHeader header, ReadOnlySpan<byte> payload)
    {
        if (header.SessionId != 0 ||
            !HandshakePayloadSerializer.TryReadServerReject(payload, out ServerReject? reject))
        {
            RejectLocally(ServerRejectReason.MalformedPacket, "Server reject payload was invalid.");
            return;
        }

        Rejection = reject;
        State = NetworkHandshakeClientState.Rejected;
    }

    private void RejectLocally(ServerRejectReason reason, string detail)
    {
        Rejection = new ServerReject(reason, detail);
        State = NetworkHandshakeClientState.Rejected;
    }

    private void SendPacket(ProtocolMessageType messageType, ulong sessionId, ReadOnlySpan<byte> payload)
    {
        Span<byte> packet = stackalloc byte[ProtocolConstants.PacketHeaderSize + HandshakePayloadSerializer.MaxClientHelloPayloadSize];
        ProtocolPacketHeader header = ProtocolPacketHeader.Create(
            sessionId,
            messageType,
            nextSequence++,
            acknowledgedSequence: 0,
            acknowledgementMask: 0);

        if (!ProtocolPacketFramer.TryWritePacket(
            header,
            payload,
            packet,
            out int bytesWritten,
            out ProtocolFrameError error))
        {
            throw new InvalidOperationException($"Client handshake packet could not be framed: {error}.");
        }

        transport.Send(serverPeerId, packet[..bytesWritten], NetworkDelivery.ReliableOrdered, channel: 0);
    }
}

public enum NetworkHandshakeClientState
{
    Pending,
    Accepted,
    Rejected,
    Disconnected,
}
