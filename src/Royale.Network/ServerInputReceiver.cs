using System.Net.Sockets;
using Royale.Protocol;

namespace Royale.Network;

public sealed class ServerInputReceiver : INetworkEventHandler
{
    private readonly IReadOnlyDictionary<NetworkPeerId, ServerAccept> acceptedPeers;
    private readonly Action<NetworkPeerId, ServerAccept, PlayerInputCommand> acceptCommand;
    private readonly Dictionary<NetworkPeerId, uint> lastAcceptedSequences = [];

    public ServerInputReceiver(
        IReadOnlyDictionary<NetworkPeerId, ServerAccept> acceptedPeers,
        Action<NetworkPeerId, ServerAccept, PlayerInputCommand> acceptCommand)
    {
        this.acceptedPeers = acceptedPeers;
        this.acceptCommand = acceptCommand;
    }

    public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
    }

    public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
    {
        lastAcceptedSequences.Remove(peerId);
    }

    public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        if (channel != ClientInputSender.InputChannel ||
            !acceptedPeers.TryGetValue(peerId, out ServerAccept? accept) ||
            accept is null ||
            accept.SessionId == 0)
        {
            return;
        }

        if (!ProtocolPacketFramer.TryReadPacket(
            packet.Span,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out _) ||
            header.MessageType != ProtocolMessageType.ClientInput ||
            header.SessionId != accept.SessionId ||
            !ClientInputPayloadSerializer.TryReadCommands(payload, out PlayerInputCommand[] commands))
        {
            return;
        }

        Array.Sort(commands, static (left, right) => left.Sequence.CompareTo(right.Sequence));

        bool hasLastAccepted = lastAcceptedSequences.TryGetValue(peerId, out uint lastAcceptedSequence);
        foreach (PlayerInputCommand command in commands)
        {
            if (hasLastAccepted && command.Sequence <= lastAcceptedSequence)
                continue;

            acceptCommand(peerId, accept, command);
            lastAcceptedSequence = command.Sequence;
            hasLastAccepted = true;
        }

        if (hasLastAccepted)
            lastAcceptedSequences[peerId] = lastAcceptedSequence;
    }

    public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
    {
    }

    public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
    {
    }
}
