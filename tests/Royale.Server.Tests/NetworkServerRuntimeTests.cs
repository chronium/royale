using System.Numerics;
using Royale.Content;
using Royale.Network;
using Royale.Protocol;
using Royale.Server;

namespace Royale.Server.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class NetworkServerRuntimeTests
{
    [Fact]
    public void HandshakeAllocatesAuthoritativePlayerAndSendsAcceptedSnapshot()
    {
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(1);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId));

        transport.QueueConnected(peer);
        transport.QueuePacket(peer, FrameClientHello(), NetworkDelivery.ReliableOrdered, channel: 0);
        runtime.Step();

        Assert.Equal(1, runtime.ConnectedClientCount);
        Assert.Equal(1, runtime.ActivePlayerCount);
        ServerAccept accept = ReadAccept(Assert.Single(transport.SentPackets).Payload);
        Assert.Equal(1U, accept.ConnectionId);
        Assert.Equal(1U, accept.PlayerId);

        runtime.Step();
        runtime.Step();

        SentPacket snapshotPacket = Assert.Single(
            transport.SentPackets,
            packet => ReadHeader(packet.Payload).MessageType == ProtocolMessageType.ServerSnapshot);
        ServerSnapshot snapshot = ReadSnapshot(snapshotPacket.Payload);
        Assert.Equal(3UL, snapshot.ServerTick);
        Assert.Equal(accept.PlayerId, snapshot.LocalPlayerId);
        Assert.Single(snapshot.Players);
    }

    [Fact]
    public void ReceivedClientInputIsAcknowledgedInLaterSnapshot()
    {
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(1);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(CreateOpenArenaMap()));

        ServerAccept accept = ConnectClient(runtime, transport, peer);
        transport.SentPackets.Clear();
        transport.QueuePacket(
            peer,
            FrameInputPacket(accept.SessionId, ValidCommand(sequence: 5) with
            {
                Move = new Vector2(0.0f, 1.0f),
                YawRadians = MathF.PI / 2.0f,
            }),
            NetworkDelivery.Sequenced,
            ClientInputSender.InputChannel);

        runtime.Step();
        runtime.Step();

        SentPacket snapshotPacket = Assert.Single(transport.SentPackets);
        ServerSnapshot snapshot = ReadSnapshot(snapshotPacket.Payload);
        PlayerSnapshotState player = Assert.Single(snapshot.Players);
        Assert.Equal(5U, snapshot.AcknowledgedInputSequence);
        Assert.True(player.Position.X > 0.01f);
    }

    [Fact]
    public void DisconnectRemovesPeerAndAuthoritativePlayer()
    {
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(1);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId));

        _ = ConnectClient(runtime, transport, peer);

        transport.QueueDisconnected(peer);
        runtime.Step();

        Assert.Empty(runtime.AcceptedPeers);
        Assert.Equal(0, runtime.ConnectedClientCount);
        Assert.Equal(0, runtime.ActivePlayerCount);
    }

    private static ServerAccept ConnectClient(
        NetworkServerRuntime runtime,
        FakeNetworkTransport transport,
        NetworkPeerId peer)
    {
        transport.QueueConnected(peer);
        transport.QueuePacket(peer, FrameClientHello(), NetworkDelivery.ReliableOrdered, channel: 0);
        runtime.Step();
        return ReadAccept(Assert.Single(transport.SentPackets).Payload);
    }

    private static byte[] FrameClientHello()
    {
        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxClientHelloPayloadSize];
        Assert.True(HandshakePayloadSerializer.TryWriteClientHello(
            new ClientHello(ProtocolConstants.BuildId, ProtocolConstants.ContentVersion),
            payload,
            out int bytesWritten));
        return FramePacket(ProtocolMessageType.ClientHello, sessionId: 0, payload[..bytesWritten]);
    }

    private static byte[] FrameInputPacket(ulong sessionId, PlayerInputCommand command)
    {
        Span<byte> payload = stackalloc byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize];
        Assert.True(ClientInputPayloadSerializer.TryWriteCommands([command], payload, out int bytesWritten));
        return FramePacket(ProtocolMessageType.ClientInput, sessionId, payload[..bytesWritten]);
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

    private static ProtocolPacketHeader ReadHeader(ReadOnlySpan<byte> packet)
    {
        Assert.True(ProtocolPacketFramer.TryReadPacket(
            packet,
            out ProtocolPacketHeader header,
            out _,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        return header;
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
        return accept!;
    }

    private static ServerSnapshot ReadSnapshot(ReadOnlySpan<byte> packet)
    {
        Assert.True(ProtocolPacketFramer.TryReadPacket(
            packet,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        Assert.Equal(ProtocolMessageType.ServerSnapshot, header.MessageType);
        Assert.True(ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out ServerSnapshot? snapshot));
        Assert.NotNull(snapshot);
        return snapshot!;
    }

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);

    private static GameMap CreateOpenArenaMap() => new()
    {
        Id = "network-runtime-arena",
        Name = "Network Runtime Arena",
        SpawnPoints =
        [
            new MapSpawnPoint
            {
                Id = "spawn-a",
                Position = new MapVector3(0.0f, 0.0f, 0.0f),
            },
        ],
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "floor",
                Position = new MapVector3(0.0f, -0.1f, 0.0f),
                Size = new MapVector3(30.0f, 0.2f, 30.0f),
            },
        ],
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, 0.0f),
            Radius = 50.0f,
        },
    };

    private sealed class FakeNetworkTransport : INetworkTransport
    {
        private readonly Queue<Action<INetworkEventHandler>> events = [];

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
            QueueDisconnected(peerId);
        }

        public void Poll(INetworkEventHandler handler)
        {
            while (events.TryDequeue(out Action<INetworkEventHandler>? queuedEvent))
                queuedEvent(handler);
        }

        public void Dispose()
        {
        }

        public void QueueConnected(NetworkPeerId peerId)
        {
            events.Enqueue(handler => handler.Connected(peerId, new NetworkEndpoint("127.0.0.1", 7777)));
        }

        public void QueueDisconnected(NetworkPeerId peerId)
        {
            events.Enqueue(handler => handler.Disconnected(peerId, NetworkDisconnectReason.RemoteConnectionClose));
        }

        public void QueuePacket(
            NetworkPeerId peerId,
            byte[] packet,
            NetworkDelivery delivery,
            byte channel)
        {
            events.Enqueue(handler => handler.PacketReceived(peerId, packet, delivery, channel));
        }
    }

    private readonly record struct SentPacket(
        NetworkPeerId PeerId,
        byte[] Payload,
        NetworkDelivery Delivery,
        byte Channel);
}
