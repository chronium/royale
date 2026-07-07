using Royale.Content;
using Royale.Network;
using Royale.Protocol;
using Royale.Server;
using System.Numerics;

namespace Royale.Server.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class InProcessServerHandshakeTests
{
    [Fact]
    public void HandshakeAcceptCallbackCanUseInProcessServerSessionConnectClient()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        FakeNetworkTransport transport = new();
        var handshake = new NetworkHandshakeServer(
            transport,
            _ =>
            {
                InProcessClientConnection connection = session.ConnectClient();
                return new NetworkHandshakeAcceptResult(
                    connection.ConnectionId.Value,
                    connection.PlayerId.Value,
                    session.CurrentTick,
                    session.MapId);
            });

        handshake.PacketReceived(
            new NetworkPeerId(1),
            FrameClientHello(),
            NetworkDelivery.ReliableOrdered,
            channel: 0);

        ServerAccept accept = ReadAccept(Assert.Single(transport.SentPackets).Payload);
        Assert.Equal(1U, accept.ConnectionId);
        Assert.Equal(1U, accept.PlayerId);
        Assert.Equal(0UL, accept.ServerTick);
        Assert.Equal(ContentCatalog.DefaultMapId, accept.MapId);
        Assert.NotEqual(0UL, accept.SessionId);
        Assert.Equal(1, session.ConnectedClientCount);
        Assert.Equal(1, session.ActivePlayerCount);

        InProcessClientConnection client = new(new ServerConnectionId(accept.ConnectionId), new ServerPlayerId(accept.PlayerId));
        Assert.True(session.TryDequeueSnapshot(client, out ServerSnapshot? snapshot));
        Assert.NotNull(snapshot);
        Assert.Equal(accept.PlayerId, snapshot.LocalPlayerId);
    }

    [Fact]
    public void SnapshotSenderCanUseAcceptedHandshakePeerAndInProcessSnapshotQueue()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        FakeNetworkTransport transport = new();
        var handshake = new NetworkHandshakeServer(
            transport,
            _ =>
            {
                InProcessClientConnection connection = session.ConnectClient();
                return new NetworkHandshakeAcceptResult(
                    connection.ConnectionId.Value,
                    connection.PlayerId.Value,
                    session.CurrentTick,
                    session.MapId);
            });
        NetworkPeerId peerId = new(1);

        handshake.PacketReceived(
            peerId,
            FrameClientHello(),
            NetworkDelivery.ReliableOrdered,
            channel: 0);
        ServerAccept accept = ReadAccept(Assert.Single(transport.SentPackets).Payload);
        InProcessClientConnection client = new(
            new ServerConnectionId(accept.ConnectionId),
            new ServerPlayerId(accept.PlayerId));
        _ = session.DrainSnapshots(client);
        Assert.True(session.TryEnqueueInputCommand(client, ValidCommand(sequence: 9)));
        session.Step();
        session.Step();
        session.Step();
        transport.SentPackets.Clear();
        var sender = new ServerSnapshotSender(
            transport,
            handshake.AcceptedPeers,
            (_, accepted) =>
            {
                var recipient = new InProcessClientConnection(
                    new ServerConnectionId(accepted.ConnectionId),
                    new ServerPlayerId(accepted.PlayerId));
                IReadOnlyList<ServerSnapshot> snapshots = session.DrainSnapshots(recipient);
                return snapshots.Count == 0 ? null : snapshots[^1];
            });

        int sent = sender.SendDueSnapshots(session.CurrentTick);

        SentPacket packet = Assert.Single(transport.SentPackets);
        Assert.Equal(1, sent);
        Assert.Equal(peerId, packet.PeerId);
        Assert.Equal(NetworkDelivery.Sequenced, packet.Delivery);
        Assert.Equal(ServerSnapshotSender.SnapshotChannel, packet.Channel);
        Assert.True(ProtocolPacketFramer.TryReadPacket(
            packet.Payload,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        Assert.Equal(ProtocolMessageType.ServerSnapshot, header.MessageType);
        Assert.Equal(accept.SessionId, header.SessionId);
        Assert.True(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out ServerSnapshot? snapshot));
        Assert.NotNull(snapshot);
        Assert.Equal(accept.PlayerId, snapshot!.LocalPlayerId);
        Assert.Equal(3UL, snapshot.ServerTick);
        Assert.Equal(9U, snapshot.AcknowledgedInputSequence);
    }

    [Fact]
    public void InputReceiverCanQueueHandshakePeerInputIntoInProcessSession()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        FakeNetworkTransport serverTransport = new();
        var handshake = new NetworkHandshakeServer(
            serverTransport,
            _ =>
            {
                InProcessClientConnection connection = session.ConnectClient();
                return new NetworkHandshakeAcceptResult(
                    connection.ConnectionId.Value,
                    connection.PlayerId.Value,
                    session.CurrentTick,
                    session.MapId);
            });
        NetworkPeerId peerId = new(1);
        handshake.PacketReceived(
            peerId,
            FrameClientHello(),
            NetworkDelivery.ReliableOrdered,
            channel: 0);
        ServerAccept accept = ReadAccept(Assert.Single(serverTransport.SentPackets).Payload);
        InProcessClientConnection client = new(
            new ServerConnectionId(accept.ConnectionId),
            new ServerPlayerId(accept.PlayerId));
        _ = session.DrainSnapshots(client);
        var receiver = new ServerInputReceiver(
            handshake.AcceptedPeers,
            (_, accepted, command) =>
            {
                var recipient = new InProcessClientConnection(
                    new ServerConnectionId(accepted.ConnectionId),
                    new ServerPlayerId(accepted.PlayerId));
                Assert.True(session.TryEnqueueInputCommand(recipient, command));
            });
        FakeNetworkTransport clientTransport = new();
        var inputSender = new ClientInputSender(clientTransport, peerId, accept);

        Assert.True(inputSender.TrySend(ValidCommand(sequence: 12)));
        SentPacket inputPacket = Assert.Single(clientTransport.SentPackets);
        receiver.PacketReceived(peerId, inputPacket.Payload, inputPacket.Delivery, inputPacket.Channel);
        session.Step();

        ServerSnapshot snapshot = session.DrainSnapshots(client)[^1];
        Assert.Equal(12U, snapshot.AcknowledgedInputSequence);
    }

    private static byte[] FrameClientHello()
    {
        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxClientHelloPayloadSize];
        Assert.True(HandshakePayloadSerializer.TryWriteClientHello(
            new ClientHello(ProtocolConstants.BuildId, ProtocolConstants.ContentVersion),
            payload,
            out int payloadBytesWritten));

        byte[] packet = new byte[ProtocolConstants.PacketHeaderSize + payloadBytesWritten];
        ProtocolPacketHeader header = ProtocolPacketHeader.Create(
            sessionId: 0,
            messageType: ProtocolMessageType.ClientHello,
            sequence: 1,
            acknowledgedSequence: 0,
            acknowledgementMask: 0);

        Assert.True(ProtocolPacketFramer.TryWritePacket(
            header,
            payload[..payloadBytesWritten],
            packet,
            out int packetBytesWritten,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        Assert.Equal(packet.Length, packetBytesWritten);
        return packet;
    }

    private static ServerAccept ReadAccept(ReadOnlySpan<byte> packet)
    {
        Assert.True(ProtocolPacketFramer.TryReadPacket(
            packet,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        Assert.Equal(ProtocolMessageType.ServerAccept, header.MessageType);
        Assert.True(HandshakePayloadSerializer.TryReadServerAccept(payload, out ServerAccept? accept));
        Assert.NotNull(accept);
        Assert.Equal(header.SessionId, accept!.SessionId);
        return accept;
    }

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);

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
}
