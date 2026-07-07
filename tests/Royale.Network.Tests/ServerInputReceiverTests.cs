using System.Numerics;
using Royale.Network;
using Royale.Protocol;

namespace Royale.Network.Tests;

public sealed class ServerInputReceiverTests
{
    [Fact]
    public void ReceiverForwardsValidCommandsOldestToNewest()
    {
        NetworkPeerId peer = new(1);
        ServerAccept accept = Accept(sessionId: 10, playerId: 20);
        var acceptedPeers = new Dictionary<NetworkPeerId, ServerAccept> { [peer] = accept };
        List<uint> receivedSequences = [];
        var receiver = new ServerInputReceiver(
            acceptedPeers,
            (_, _, command) => receivedSequences.Add(command.Sequence));

        receiver.PacketReceived(
            peer,
            FrameInputPacket(accept.SessionId, [ValidCommand(3), ValidCommand(2), ValidCommand(1)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);

        Assert.Equal([1U, 2U, 3U], receivedSequences);
    }

    [Fact]
    public void ReceiverRejectsPreSessionAndWrongSessionPackets()
    {
        NetworkPeerId peer = new(1);
        ServerAccept accept = Accept(sessionId: 10, playerId: 20);
        var receiver = new ServerInputReceiver(
            new Dictionary<NetworkPeerId, ServerAccept> { [peer] = accept },
            (_, _, _) => throw new InvalidOperationException("Input should not be accepted."));

        receiver.PacketReceived(
            new NetworkPeerId(99),
            FrameInputPacket(accept.SessionId, [ValidCommand(1)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
        receiver.PacketReceived(
            peer,
            FrameInputPacket(sessionId: 0, [ValidCommand(1)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
        receiver.PacketReceived(
            peer,
            FrameInputPacket(sessionId: 11, [ValidCommand(1)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
    }

    [Fact]
    public void ReceiverRejectsWrongChannelWrongMessageAndMalformedPayload()
    {
        NetworkPeerId peer = new(1);
        ServerAccept accept = Accept(sessionId: 10, playerId: 20);
        var receiver = new ServerInputReceiver(
            new Dictionary<NetworkPeerId, ServerAccept> { [peer] = accept },
            (_, _, _) => throw new InvalidOperationException("Input should not be accepted."));

        receiver.PacketReceived(
            peer,
            FrameInputPacket(accept.SessionId, [ValidCommand(1)]),
            NetworkDelivery.Unreliable,
            channel: 0);
        receiver.PacketReceived(
            peer,
            FramePacket(ProtocolMessageType.ServerSnapshot, accept.SessionId, []),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
        receiver.PacketReceived(
            peer,
            FramePacket(ProtocolMessageType.ClientInput, accept.SessionId, [0]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
    }

    [Fact]
    public void ReceiverRejectsStaleAndDuplicateInputCommands()
    {
        NetworkPeerId peer = new(1);
        ServerAccept accept = Accept(sessionId: 10, playerId: 20);
        List<uint> receivedSequences = [];
        var receiver = new ServerInputReceiver(
            new Dictionary<NetworkPeerId, ServerAccept> { [peer] = accept },
            (_, _, command) => receivedSequences.Add(command.Sequence));

        receiver.PacketReceived(
            peer,
            FrameInputPacket(accept.SessionId, [ValidCommand(2), ValidCommand(1)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
        receiver.PacketReceived(
            peer,
            FrameInputPacket(accept.SessionId, [ValidCommand(3), ValidCommand(2), ValidCommand(2)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);

        Assert.Equal([1U, 2U, 3U], receivedSequences);
    }

    [Fact]
    public void ReceiverTracksLastAcceptedInputSequencePerPeer()
    {
        NetworkPeerId firstPeer = new(1);
        NetworkPeerId secondPeer = new(2);
        ServerAccept firstAccept = Accept(sessionId: 10, playerId: 20);
        ServerAccept secondAccept = Accept(sessionId: 11, playerId: 21);
        var acceptedPeers = new Dictionary<NetworkPeerId, ServerAccept>
        {
            [firstPeer] = firstAccept,
            [secondPeer] = secondAccept,
        };
        List<(NetworkPeerId PeerId, uint Sequence)> received = [];
        var receiver = new ServerInputReceiver(
            acceptedPeers,
            (peerId, _, command) => received.Add((peerId, command.Sequence)));

        receiver.PacketReceived(
            firstPeer,
            FrameInputPacket(firstAccept.SessionId, [ValidCommand(5)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
        receiver.PacketReceived(
            secondPeer,
            FrameInputPacket(secondAccept.SessionId, [ValidCommand(3)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
        receiver.PacketReceived(
            firstPeer,
            FrameInputPacket(firstAccept.SessionId, [ValidCommand(4)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);
        receiver.PacketReceived(
            secondPeer,
            FrameInputPacket(secondAccept.SessionId, [ValidCommand(4)]),
            NetworkDelivery.Unreliable,
            ClientInputSender.InputChannel);

        Assert.Equal(
            [(firstPeer, 5U), (secondPeer, 3U), (secondPeer, 4U)],
            received);
    }

    private static ServerAccept Accept(ulong sessionId, uint playerId) => new(
        sessionId,
        ConnectionId: playerId + 100,
        playerId,
        ServerTick: 0,
        MapId: "graybox");

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);

    private static byte[] FrameInputPacket(ulong sessionId, PlayerInputCommand[] commands)
    {
        Span<byte> payload = stackalloc byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize];
        Assert.True(ClientInputPayloadSerializer.TryWriteCommands(commands, payload, out int payloadBytesWritten));
        return FramePacket(ProtocolMessageType.ClientInput, sessionId, payload[..payloadBytesWritten]);
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
}
