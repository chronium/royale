using System.Net.Sockets;
using System.Text;
using Royale.Protocol;

namespace Royale.Network;

public sealed class NetworkHandshakeServer : INetworkEventHandler
{
    private readonly INetworkTransport transport;
    private readonly Func<NetworkPeerId, NetworkHandshakeAdmissionResult> admitClient;
    private readonly Action<NetworkPeerId, ServerRejectReason, string>? rejectObserver;
    private readonly Dictionary<NetworkPeerId, ServerAccept> acceptedPeers = [];
    private readonly string expectedBuildId;
    private readonly string expectedContentVersion;
    private ulong nextSessionId = 1;
    private uint nextSequence = 1;

    public NetworkHandshakeServer(
        INetworkTransport transport,
        Func<NetworkPeerId, NetworkHandshakeAdmissionResult> admitClient,
        Action<NetworkPeerId, ServerRejectReason, string>? rejectObserver = null,
        string expectedBuildId = ProtocolConstants.BuildId,
        string expectedContentVersion = ProtocolConstants.ContentVersion)
    {
        this.transport = transport;
        this.admitClient = admitClient;
        this.rejectObserver = rejectObserver;
        this.expectedBuildId = expectedBuildId;
        this.expectedContentVersion = expectedContentVersion;
    }

    public IReadOnlyDictionary<NetworkPeerId, ServerAccept> AcceptedPeers => acceptedPeers;

    public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
    }

    public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
    {
        acceptedPeers.Remove(peerId);
    }

    public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        if (!ProtocolPacketFramer.TryReadPacket(
            packet.Span,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error))
        {
            SendReject(peerId, RejectReasonForFrameError(error), $"Handshake frame was invalid: {error}.");
            return;
        }

        if (header.MessageType != ProtocolMessageType.ClientHello || header.SessionId != 0)
        {
            if (!acceptedPeers.ContainsKey(peerId))
                SendReject(peerId, ServerRejectReason.UnexpectedMessageType, "Expected ClientHello before session acceptance.");
            return;
        }

        if (acceptedPeers.TryGetValue(peerId, out ServerAccept? existingAccept))
        {
            SendAccept(peerId, existingAccept);
            return;
        }

        if (!HandshakePayloadSerializer.TryReadClientHello(payload, out ClientHello? hello))
        {
            SendReject(peerId, ServerRejectReason.MalformedPacket, "ClientHello payload was invalid.");
            return;
        }

        ArgumentNullException.ThrowIfNull(hello);

        if (!string.Equals(hello.BuildId, expectedBuildId, StringComparison.Ordinal))
        {
            SendReject(peerId, ServerRejectReason.IncompatibleBuild, $"Build '{hello.BuildId}' is not supported.");
            return;
        }

        if (!string.Equals(hello.ContentVersion, expectedContentVersion, StringComparison.Ordinal))
        {
            SendReject(peerId, ServerRejectReason.IncompatibleContent, $"Content version '{hello.ContentVersion}' is not supported.");
            return;
        }

        ServerAccept accept;
        try
        {
            NetworkHandshakeAdmissionResult admission = admitClient(peerId);
            if (admission.Rejection is ServerReject rejection)
            {
                SendReject(peerId, rejection.Reason, rejection.Detail);
                return;
            }

            NetworkHandshakeAcceptResult result = admission.Acceptance
                ?? throw new InvalidOperationException("Handshake admission returned neither acceptance nor rejection.");
            ulong sessionId = AllocateSessionId();
            accept = new ServerAccept(
                sessionId,
                result.ConnectionId,
                result.PlayerId,
                result.ServerTick,
                result.MapId);

            Span<byte> validationBuffer = stackalloc byte[HandshakePayloadSerializer.MaxServerAcceptPayloadSize];
            if (accept.SessionId == 0 ||
                accept.ConnectionId == 0 ||
                accept.PlayerId == 0 ||
                !HandshakePayloadSerializer.TryWriteServerAccept(accept, validationBuffer, out _))
            {
                SendReject(peerId, ServerRejectReason.AcceptFailed, "Server accept result was invalid.");
                return;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SendReject(peerId, ServerRejectReason.AcceptFailed, "Server failed to accept the client.");
            return;
        }

        acceptedPeers.Add(peerId, accept);
        SendAccept(peerId, accept);
    }

    public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
    {
    }

    public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
    {
    }

    private ulong AllocateSessionId()
    {
        while (nextSessionId == 0)
            nextSessionId++;

        return nextSessionId++;
    }

    private void SendAccept(NetworkPeerId peerId, ServerAccept accept)
    {
        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxServerAcceptPayloadSize];
        if (!HandshakePayloadSerializer.TryWriteServerAccept(accept, payload, out int payloadBytesWritten))
            throw new InvalidOperationException("Server accept payload could not be serialized.");

        SendPacket(peerId, ProtocolMessageType.ServerAccept, accept.SessionId, payload[..payloadBytesWritten]);
    }

    private void SendReject(NetworkPeerId peerId, ServerRejectReason reason, string detail)
    {
        rejectObserver?.Invoke(peerId, reason, detail);

        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxServerRejectPayloadSize];
        ServerReject reject = new(reason, TruncateUtf8(detail, ProtocolConstants.MaxRejectDetailLength));

        if (!HandshakePayloadSerializer.TryWriteServerReject(reject, payload, out int payloadBytesWritten))
            throw new InvalidOperationException("Server reject payload could not be serialized.");

        SendPacket(peerId, ProtocolMessageType.ServerReject, sessionId: 0, payload[..payloadBytesWritten]);
    }

    private void SendPacket(
        NetworkPeerId peerId,
        ProtocolMessageType messageType,
        ulong sessionId,
        ReadOnlySpan<byte> payload)
    {
        Span<byte> packet = stackalloc byte[ProtocolConstants.PacketHeaderSize + HandshakePayloadSerializer.MaxServerRejectPayloadSize];
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
            throw new InvalidOperationException($"Server handshake packet could not be framed: {error}.");
        }

        transport.Send(peerId, packet[..bytesWritten], NetworkDelivery.ReliableOrdered, channel: 0);
    }

    private static ServerRejectReason RejectReasonForFrameError(ProtocolFrameError error) =>
        error == ProtocolFrameError.UnsupportedMajorVersion
            ? ServerRejectReason.UnsupportedProtocolVersion
            : ServerRejectReason.MalformedPacket;

    private static string TruncateUtf8(string value, int maxByteLength)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxByteLength)
            return value;

        int length = 0;
        int bytes = 0;
        foreach (Rune rune in value.EnumerateRunes())
        {
            int runeBytes = rune.Utf8SequenceLength;
            if (bytes + runeBytes > maxByteLength)
                break;

            bytes += runeBytes;
            length += rune.Utf16SequenceLength;
        }

        return value[..length];
    }
}

public readonly record struct NetworkHandshakeAcceptResult(
    uint ConnectionId,
    uint PlayerId,
    ulong ServerTick,
    string MapId);

public readonly record struct NetworkHandshakeAdmissionResult
{
    private NetworkHandshakeAdmissionResult(
        NetworkHandshakeAcceptResult? acceptance,
        ServerReject? rejection)
    {
        Acceptance = acceptance;
        Rejection = rejection;
    }

    public NetworkHandshakeAcceptResult? Acceptance { get; }

    public ServerReject? Rejection { get; }

    public static NetworkHandshakeAdmissionResult Accepted(NetworkHandshakeAcceptResult acceptance) =>
        new(acceptance, rejection: null);

    public static NetworkHandshakeAdmissionResult Rejected(ServerRejectReason reason, string detail) =>
        new(acceptance: null, new ServerReject(reason, detail));

    public static implicit operator NetworkHandshakeAdmissionResult(NetworkHandshakeAcceptResult acceptance) =>
        Accepted(acceptance);
}
