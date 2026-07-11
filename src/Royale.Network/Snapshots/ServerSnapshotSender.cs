using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

using Royale.Network.Transport;

namespace Royale.Network.Snapshots;

public sealed class ServerSnapshotSender
{
    public const byte SnapshotChannel = 1;
    public const int SimulationTicksPerSnapshot = 3;

    private readonly INetworkTransport transport;
    private readonly IReadOnlyDictionary<NetworkPeerId, ServerAccept> acceptedPeers;
    private readonly Func<NetworkPeerId, ServerAccept, ServerSnapshot?> snapshotProvider;
    private uint nextSequence = 1;

    public ServerSnapshotSender(
        INetworkTransport transport,
        IReadOnlyDictionary<NetworkPeerId, ServerAccept> acceptedPeers,
        Func<NetworkPeerId, ServerAccept, ServerSnapshot?> snapshotProvider)
    {
        this.transport = transport;
        this.acceptedPeers = acceptedPeers;
        this.snapshotProvider = snapshotProvider;
    }

    public int SendDueSnapshots(ulong serverTick)
    {
        if (serverTick % SimulationTicksPerSnapshot != 0)
            return 0;

        int sent = 0;
        foreach ((NetworkPeerId peerId, ServerAccept accept) in acceptedPeers)
        {
            if (accept.SessionId == 0)
                continue;

            ServerSnapshot? snapshot = snapshotProvider(peerId, accept);
            if (snapshot is null)
                continue;

            SendSnapshot(peerId, accept.SessionId, snapshot);
            sent++;
        }

        return sent;
    }

    private void SendSnapshot(NetworkPeerId peerId, ulong sessionId, ServerSnapshot snapshot)
    {
        byte[] payload = new byte[ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize];
        if (!ServerSnapshotPayloadSerializer.TryWriteSnapshot(snapshot, payload, out int payloadBytesWritten))
            throw new InvalidOperationException("Server snapshot payload could not be serialized.");

        byte[] packet = new byte[ProtocolConstants.PacketHeaderSize + payloadBytesWritten];
        ProtocolPacketHeader header = ProtocolPacketHeader.Create(
            sessionId,
            ProtocolMessageType.ServerSnapshot,
            nextSequence++,
            acknowledgedSequence: 0,
            acknowledgementMask: 0);

        if (!ProtocolPacketFramer.TryWritePacket(
            header,
            payload.AsSpan(0, payloadBytesWritten),
            packet,
            out int packetBytesWritten,
            out ProtocolFrameError error))
        {
            throw new InvalidOperationException($"Server snapshot packet could not be framed: {error}.");
        }

        transport.Send(
            peerId,
            packet.AsSpan(0, packetBytesWritten),
            NetworkDelivery.Sequenced,
            SnapshotChannel);
    }
}
