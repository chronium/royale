using System.Numerics;
using Royale.Network;
using Royale.Protocol;

namespace Royale.Network.Tests;

public sealed class ClientInputSenderTests
{
    [Fact]
    public void SenderFramesClientInputWithAcceptedSessionAndSequencedInputChannel()
    {
        FakeNetworkTransport transport = new();
        NetworkPeerId serverPeer = new(7);
        ServerAccept accept = Accept(sessionId: 44);
        var sender = new ClientInputSender(transport, serverPeer, accept);

        Assert.True(sender.TrySend(ValidCommand(sequence: 10)));

        SentPacket sent = Assert.Single(transport.SentPackets);
        PacketParts parts = ReadPacket(sent.Payload);
        Assert.Equal(serverPeer, sent.PeerId);
        Assert.Equal(NetworkDelivery.Sequenced, sent.Delivery);
        Assert.Equal(ClientInputSender.InputChannel, sent.Channel);
        Assert.Equal(accept.SessionId, parts.Header.SessionId);
        Assert.Equal(ProtocolMessageType.ClientInput, parts.Header.MessageType);
        Assert.Equal(1U, parts.Header.Sequence);
        Assert.True(ClientInputPayloadSerializer.TryReadCommands(parts.Payload, out PlayerInputCommand[] commands));
        Assert.Equal(10U, Assert.Single(commands).Sequence);
    }

    [Fact]
    public void SenderUsesMonotonicPacketSequences()
    {
        FakeNetworkTransport transport = new();
        var sender = new ClientInputSender(transport, new NetworkPeerId(7), Accept(sessionId: 44));

        Assert.True(sender.TrySend(ValidCommand(sequence: 1)));
        Assert.True(sender.TrySend(ValidCommand(sequence: 2)));

        Assert.Equal(1U, ReadPacket(transport.SentPackets[0].Payload).Header.Sequence);
        Assert.Equal(2U, ReadPacket(transport.SentPackets[1].Payload).Header.Sequence);
    }

    [Fact]
    public void SenderIncludesNewestCommandPlusThreeRecentCommandsNewestFirst()
    {
        FakeNetworkTransport transport = new();
        var sender = new ClientInputSender(transport, new NetworkPeerId(7), Accept(sessionId: 44));

        for (uint sequence = 1; sequence <= 5; sequence++)
            Assert.True(sender.TrySend(ValidCommand(sequence)));

        PacketParts parts = ReadPacket(transport.SentPackets[^1].Payload);
        Assert.True(ClientInputPayloadSerializer.TryReadCommands(parts.Payload, out PlayerInputCommand[] commands));
        Assert.Equal([5U, 4U, 3U, 2U], commands.Select(command => command.Sequence).ToArray());
    }

    [Fact]
    public void SenderRejectsInvalidCommandWithoutSending()
    {
        FakeNetworkTransport transport = new();
        var sender = new ClientInputSender(transport, new NetworkPeerId(7), Accept(sessionId: 44));

        Assert.False(sender.TrySend(ValidCommand(sequence: 1) with
        {
            Move = new Vector2(2.0f, 0.0f),
        }));

        Assert.Empty(transport.SentPackets);
    }

    private static ServerAccept Accept(ulong sessionId) => new(
        sessionId,
        ConnectionId: 10,
        PlayerId: 20,
        ServerTick: 30,
        MapId: "graybox");

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);

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
