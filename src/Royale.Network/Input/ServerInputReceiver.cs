using System.Net.Sockets;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

using Royale.Network.Transport;

namespace Royale.Network.Input;

public sealed class ServerInputReceiver : INetworkEventHandler
{
    private readonly IReadOnlyDictionary<NetworkPeerId, ServerAccept> acceptedPeers;
    private readonly Action<NetworkPeerId, ServerAccept, PlayerInputCommand> acceptCommand;
    private readonly Action<NetworkPeerId, ServerInputRejectReason>? rejectObserver;
    private readonly Dictionary<NetworkPeerId, uint> lastAcceptedSequences = [];

    public ServerInputReceiver(
        IReadOnlyDictionary<NetworkPeerId, ServerAccept> acceptedPeers,
        Action<NetworkPeerId, ServerAccept, PlayerInputCommand> acceptCommand,
        Action<NetworkPeerId, ServerInputRejectReason>? rejectObserver = null)
    {
        this.acceptedPeers = acceptedPeers;
        this.acceptCommand = acceptCommand;
        this.rejectObserver = rejectObserver;
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
        if (channel != ClientInputSender.InputChannel)
            return;

        if (!acceptedPeers.TryGetValue(peerId, out ServerAccept? accept) ||
            accept is null ||
            accept.SessionId == 0)
        {
            return;
        }

        if (!ProtocolPacketFramer.TryReadPacket(
            packet.Span,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out _))
        {
            rejectObserver?.Invoke(peerId, ServerInputRejectReason.MalformedFrame);
            return;
        }

        if (header.MessageType != ProtocolMessageType.ClientInput)
        {
            rejectObserver?.Invoke(peerId, ServerInputRejectReason.UnexpectedMessageType);
            return;
        }

        if (header.SessionId != accept.SessionId)
        {
            rejectObserver?.Invoke(peerId, ServerInputRejectReason.WrongSession);
            return;
        }

        if (!ClientInputPayloadSerializer.TryReadCommands(payload, out PlayerInputCommand[] commands))
        {
            rejectObserver?.Invoke(peerId, ServerInputRejectReason.MalformedPayload);
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

public enum ServerInputRejectReason
{
    MalformedFrame,
    UnexpectedMessageType,
    WrongSession,
    MalformedPayload,
    InvalidCommand,
}
