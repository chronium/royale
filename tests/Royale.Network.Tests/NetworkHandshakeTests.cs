using Royale.Network;
using Royale.Protocol;

namespace Royale.Network.Tests;

public sealed class NetworkHandshakeTests
{
    [Fact]
    public void ClientSendsHelloWithSessionZero()
    {
        FakeNetworkTransport transport = new();
        NetworkPeerId serverPeer = new(7);

        _ = new NetworkHandshakeClient(transport, serverPeer);

        SentPacket sent = Assert.Single(transport.SentPackets);
        PacketParts parts = ReadPacket(sent.Payload);

        Assert.Equal(serverPeer, sent.PeerId);
        Assert.Equal(NetworkDelivery.ReliableOrdered, sent.Delivery);
        Assert.Equal(0, sent.Channel);
        Assert.Equal(0UL, parts.Header.SessionId);
        Assert.Equal(ProtocolMessageType.ClientHello, parts.Header.MessageType);
        Assert.True(HandshakePayloadSerializer.TryReadClientHello(parts.Payload, out ClientHello? hello));
        Assert.NotNull(hello);
        Assert.Equal(ProtocolConstants.BuildId, hello!.BuildId);
        Assert.Equal(ProtocolConstants.ContentVersion, hello.ContentVersion);
    }

    [Fact]
    public void ServerAcceptsCompatibleHelloAndClientStoresAcceptance()
    {
        FakeNetworkTransport clientTransport = new();
        FakeNetworkTransport serverTransport = new();
        NetworkPeerId clientPeer = new(1);
        NetworkPeerId serverPeer = new(2);
        int acceptCount = 0;
        var server = new NetworkHandshakeServer(
            serverTransport,
            _ =>
            {
                acceptCount++;
                return new NetworkHandshakeAcceptResult(
                    ConnectionId: 10,
                    PlayerId: 20,
                    ServerTick: 30,
                    MapId: "graybox");
            });
        var client = new NetworkHandshakeClient(clientTransport, serverPeer);

        server.PacketReceived(
            clientPeer,
            clientTransport.SentPackets.Single().Payload,
            NetworkDelivery.ReliableOrdered,
            channel: 0);
        SentPacket acceptPacket = Assert.Single(serverTransport.SentPackets);
        client.PacketReceived(serverPeer, acceptPacket.Payload, acceptPacket.Delivery, acceptPacket.Channel);

        Assert.Equal(1, acceptCount);
        Assert.Equal(NetworkHandshakeClientState.Accepted, client.State);
        Assert.NotNull(client.AcceptedSession);
        Assert.Equal(10U, client.AcceptedSession.ConnectionId);
        Assert.Equal(20U, client.AcceptedSession.PlayerId);
        Assert.Equal(30UL, client.AcceptedSession.ServerTick);
        Assert.Equal("graybox", client.AcceptedSession.MapId);
        Assert.NotEqual(0UL, client.AcceptedSession.SessionId);

        PacketParts parts = ReadPacket(acceptPacket.Payload);
        Assert.Equal(ProtocolMessageType.ServerAccept, parts.Header.MessageType);
        Assert.Equal(client.AcceptedSession.SessionId, parts.Header.SessionId);
        Assert.True(HandshakePayloadSerializer.TryReadServerAccept(parts.Payload, out ServerAccept? accept));
        Assert.Equal(client.AcceptedSession, accept);
    }

    [Fact]
    public void ServerRejectsWrongBuildId()
    {
        ServerReject reject = RunRejectedHello(new ClientHello("other-build", ProtocolConstants.ContentVersion));

        Assert.Equal(ServerRejectReason.IncompatibleBuild, reject.Reason);
    }

    [Fact]
    public void ServerRejectsWrongContentVersion()
    {
        ServerReject reject = RunRejectedHello(new ClientHello(ProtocolConstants.BuildId, "other-content"));

        Assert.Equal(ServerRejectReason.IncompatibleContent, reject.Reason);
    }

    [Fact]
    public void ServerRejectsMalformedClientHelloPayload()
    {
        FakeNetworkTransport transport = new();
        var server = new NetworkHandshakeServer(
            transport,
            _ => new NetworkHandshakeAcceptResult(1, 1, 0, "graybox"));
        byte[] malformedHello = FramePacket(
            ProtocolMessageType.ClientHello,
            sessionId: 0,
            payload: [(byte)(ProtocolConstants.MaxBuildIdLength + 1)]);

        server.PacketReceived(new NetworkPeerId(1), malformedHello, NetworkDelivery.ReliableOrdered, channel: 0);

        ServerReject reject = ReadReject(Assert.Single(transport.SentPackets).Payload);
        Assert.Equal(ServerRejectReason.MalformedPacket, reject.Reason);
    }

    [Fact]
    public void ServerRejectsNonHelloPreSessionPacket()
    {
        FakeNetworkTransport transport = new();
        var server = new NetworkHandshakeServer(
            transport,
            _ => new NetworkHandshakeAcceptResult(1, 1, 0, "graybox"));
        byte[] clientInput = FramePacket(ProtocolMessageType.ClientInput, sessionId: 0, payload: []);

        server.PacketReceived(new NetworkPeerId(1), clientInput, NetworkDelivery.ReliableOrdered, channel: 0);

        ServerReject reject = ReadReject(Assert.Single(transport.SentPackets).Payload);
        Assert.Equal(ServerRejectReason.UnexpectedMessageType, reject.Reason);
    }

    [Fact]
    public void ServerRejectsUnsupportedMajorVersion()
    {
        FakeNetworkTransport transport = new();
        var server = new NetworkHandshakeServer(
            transport,
            _ => new NetworkHandshakeAcceptResult(1, 1, 0, "graybox"));
        byte[] hello = FrameClientHello(new ClientHello(ProtocolConstants.BuildId, ProtocolConstants.ContentVersion));
        hello[4] = 2;

        server.PacketReceived(new NetworkPeerId(1), hello, NetworkDelivery.ReliableOrdered, channel: 0);

        ServerReject reject = ReadReject(Assert.Single(transport.SentPackets).Payload);
        Assert.Equal(ServerRejectReason.UnsupportedProtocolVersion, reject.Reason);
    }

    [Fact]
    public void DuplicateAcceptedPeerHelloReusesSessionWithoutAllocatingAgain()
    {
        FakeNetworkTransport transport = new();
        int acceptCount = 0;
        var server = new NetworkHandshakeServer(
            transport,
            _ =>
            {
                acceptCount++;
                return new NetworkHandshakeAcceptResult(1, 1, 0, "graybox");
            });
        NetworkPeerId peer = new(1);
        byte[] hello = FrameClientHello(new ClientHello(ProtocolConstants.BuildId, ProtocolConstants.ContentVersion));

        server.PacketReceived(peer, hello, NetworkDelivery.ReliableOrdered, channel: 0);
        server.PacketReceived(peer, hello, NetworkDelivery.ReliableOrdered, channel: 0);

        Assert.Equal(1, acceptCount);
        Assert.Equal(2, transport.SentPackets.Count);
        ServerAccept first = ReadAccept(transport.SentPackets[0].Payload);
        ServerAccept second = ReadAccept(transport.SentPackets[1].Payload);
        Assert.Equal(first, second);
    }

    private static ServerReject RunRejectedHello(ClientHello hello)
    {
        FakeNetworkTransport transport = new();
        var server = new NetworkHandshakeServer(
            transport,
            _ => new NetworkHandshakeAcceptResult(1, 1, 0, "graybox"));

        server.PacketReceived(
            new NetworkPeerId(1),
            FrameClientHello(hello),
            NetworkDelivery.ReliableOrdered,
            channel: 0);

        return ReadReject(Assert.Single(transport.SentPackets).Payload);
    }

    private static byte[] FrameClientHello(ClientHello hello)
    {
        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxClientHelloPayloadSize];
        Assert.True(HandshakePayloadSerializer.TryWriteClientHello(hello, payload, out int bytesWritten));
        return FramePacket(ProtocolMessageType.ClientHello, sessionId: 0, payload[..bytesWritten]);
    }

    private static byte[] FramePacket(
        ProtocolMessageType messageType,
        ulong sessionId,
        ReadOnlySpan<byte> payload)
    {
        byte[] packet = new byte[ProtocolConstants.PacketHeaderSize + payload.Length];
        ProtocolPacketHeader header = ProtocolPacketHeader.Create(
            sessionId,
            messageType,
            sequence: 1,
            acknowledgedSequence: 0,
            acknowledgementMask: 0);

        Assert.True(ProtocolPacketFramer.TryWritePacket(
            header,
            payload,
            packet,
            out int bytesWritten,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        Assert.Equal(packet.Length, bytesWritten);
        return packet;
    }

    private static PacketParts ReadPacket(ReadOnlySpan<byte> packet)
    {
        Assert.True(ProtocolPacketFramer.TryReadPacket(
            packet,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        return new PacketParts(header, payload.ToArray());
    }

    private static ServerAccept ReadAccept(ReadOnlySpan<byte> packet)
    {
        PacketParts parts = ReadPacket(packet);
        Assert.Equal(ProtocolMessageType.ServerAccept, parts.Header.MessageType);
        Assert.True(HandshakePayloadSerializer.TryReadServerAccept(parts.Payload, out ServerAccept? accept));
        Assert.NotNull(accept);
        Assert.Equal(parts.Header.SessionId, accept!.SessionId);
        return accept;
    }

    private static ServerReject ReadReject(ReadOnlySpan<byte> packet)
    {
        PacketParts parts = ReadPacket(packet);
        Assert.Equal(ProtocolMessageType.ServerReject, parts.Header.MessageType);
        Assert.Equal(0UL, parts.Header.SessionId);
        Assert.True(HandshakePayloadSerializer.TryReadServerReject(parts.Payload, out ServerReject? reject));
        Assert.NotNull(reject);
        return reject!;
    }

    private sealed class FakeNetworkTransport : INetworkTransport
    {
        public List<SentPacket> SentPackets { get; } = [];

        public void Start(int port)
        {
        }

        public NetworkPeerId Connect(NetworkEndpoint endpoint) => new(1);

        public void Send(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel = 0)
        {
            SentPackets.Add(new SentPacket(peerId, packet.ToArray(), delivery, channel));
        }

        public void Disconnect(NetworkPeerId peerId)
        {
        }

        public void Poll(INetworkEventHandler handler)
        {
        }

        public void Dispose()
        {
        }
    }

    private readonly record struct SentPacket(
        NetworkPeerId PeerId,
        byte[] Payload,
        NetworkDelivery Delivery,
        byte Channel);

    private readonly record struct PacketParts(
        ProtocolPacketHeader Header,
        byte[] Payload);
}
