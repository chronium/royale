using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

using Royale.Network.Transport;

namespace Royale.Network.Input;

public sealed class ClientInputSender
{
    public const byte InputChannel = 2;

    private readonly INetworkTransport transport;
    private readonly NetworkPeerId serverPeerId;
    private readonly ServerAccept acceptedSession;
    private readonly List<PlayerInputCommand> recentCommands = new(ProtocolConstants.MaxClientInputCommandsPerPacket);
    private uint nextSequence = 1;

    public ClientInputSender(
        INetworkTransport transport,
        NetworkPeerId serverPeerId,
        ServerAccept acceptedSession)
    {
        if (acceptedSession.SessionId == 0)
            throw new ArgumentException("Accepted client input session must have a nonzero session id.", nameof(acceptedSession));

        this.transport = transport;
        this.serverPeerId = serverPeerId;
        this.acceptedSession = acceptedSession;
    }

    public bool TrySend(PlayerInputCommand command)
    {
        if (!PlayerInputCommandValidation.IsValid(command))
            return false;

        recentCommands.Insert(0, command);
        if (recentCommands.Count > ProtocolConstants.MaxClientInputCommandsPerPacket)
            recentCommands.RemoveAt(ProtocolConstants.MaxClientInputCommandsPerPacket);

        Span<byte> payload = stackalloc byte[ClientInputPayloadSerializer.MaxClientInputPayloadSize];
        if (!ClientInputPayloadSerializer.TryWriteCommands(
            recentCommands.ToArray(),
            payload,
            out int payloadBytesWritten))
        {
            return false;
        }

        Span<byte> packet = stackalloc byte[
            ProtocolConstants.PacketHeaderSize + ClientInputPayloadSerializer.MaxClientInputPayloadSize];
        ProtocolPacketHeader header = ProtocolPacketHeader.Create(
            acceptedSession.SessionId,
            ProtocolMessageType.ClientInput,
            nextSequence++,
            acknowledgedSequence: 0,
            acknowledgementMask: 0);

        if (!ProtocolPacketFramer.TryWritePacket(
            header,
            payload[..payloadBytesWritten],
            packet,
            out int packetBytesWritten,
            out ProtocolFrameError error))
        {
            throw new InvalidOperationException($"Client input packet could not be framed: {error}.");
        }

        transport.Send(
            serverPeerId,
            packet[..packetBytesWritten],
            NetworkDelivery.Sequenced,
            InputChannel);
        return true;
    }
}
