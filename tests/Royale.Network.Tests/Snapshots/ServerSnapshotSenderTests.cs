using System.Numerics;
using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

namespace Royale.Network.Tests.Snapshots;

public sealed class ServerSnapshotSenderTests
{
    [Fact]
    public void SenderSkipsTicksThatAreNotOnSnapshotCadence()
    {
        FakeNetworkTransport transport = new();
        var acceptedPeers = new Dictionary<NetworkPeerId, ServerAccept>
        {
            [new NetworkPeerId(1)] = Accept(sessionId: 10, playerId: 20),
        };
        var sender = new ServerSnapshotSender(transport, acceptedPeers, (_, accept) => SnapshotFor(accept));

        Assert.Equal(0, sender.SendDueSnapshots(1));
        Assert.Equal(0, sender.SendDueSnapshots(2));

        Assert.Empty(transport.SentPackets);
    }

    [Fact]
    public void SenderSendsOnEveryThirdSimulationTick()
    {
        FakeNetworkTransport transport = new();
        var acceptedPeers = new Dictionary<NetworkPeerId, ServerAccept>
        {
            [new NetworkPeerId(1)] = Accept(sessionId: 10, playerId: 20),
        };
        var sender = new ServerSnapshotSender(transport, acceptedPeers, (_, accept) => SnapshotFor(accept));

        Assert.Equal(1, sender.SendDueSnapshots(3));
        Assert.Equal(0, sender.SendDueSnapshots(4));
        Assert.Equal(0, sender.SendDueSnapshots(5));
        Assert.Equal(1, sender.SendDueSnapshots(6));

        Assert.Equal(2, transport.SentPackets.Count);
        Assert.Equal(1U, ReadPacket(transport.SentPackets[0].Payload).Header.Sequence);
        Assert.Equal(2U, ReadPacket(transport.SentPackets[1].Payload).Header.Sequence);
    }

    [Fact]
    public void SenderFramesSnapshotWithAcceptedSessionAndSequencedChannel()
    {
        FakeNetworkTransport transport = new();
        NetworkPeerId peerId = new(3);
        ServerAccept accept = Accept(sessionId: 44, playerId: 55);
        ServerSnapshot snapshot = SnapshotFor(accept);
        var acceptedPeers = new Dictionary<NetworkPeerId, ServerAccept>
        {
            [peerId] = accept,
        };
        var sender = new ServerSnapshotSender(transport, acceptedPeers, (_, _) => snapshot);

        int sent = sender.SendDueSnapshots(12);

        SentPacket packet = Assert.Single(transport.SentPackets);
        PacketParts parts = ReadPacket(packet.Payload);
        Assert.Equal(1, sent);
        Assert.Equal(peerId, packet.PeerId);
        Assert.Equal(NetworkDelivery.Sequenced, packet.Delivery);
        Assert.Equal(ServerSnapshotSender.SnapshotChannel, packet.Channel);
        Assert.Equal(accept.SessionId, parts.Header.SessionId);
        Assert.Equal(ProtocolMessageType.ServerSnapshot, parts.Header.MessageType);
        Assert.Equal(1U, parts.Header.Sequence);
        Assert.Equal(0U, parts.Header.AcknowledgedSequence);
        Assert.Equal(0U, parts.Header.AcknowledgementMask);
        Assert.True(ServerSnapshotPayloadSerializer.TryReadSnapshot(parts.Payload, out ServerSnapshot? decoded));
        Assert.NotNull(decoded);
        Assert.Equal(snapshot.ServerTick, decoded!.ServerTick);
        Assert.Equal(snapshot.LocalPlayerId, decoded.LocalPlayerId);
        Assert.Equal(snapshot.AcknowledgedInputSequence, decoded.AcknowledgedInputSequence);
        Assert.Equal(snapshot.Players, decoded.Players);
        Assert.Equal(snapshot.Match, decoded.Match);
        Assert.Equal(snapshot.SafeZone, decoded.SafeZone);
    }

    [Fact]
    public void SenderSkipsPeersWithoutUsableSnapshotsOrSessions()
    {
        FakeNetworkTransport transport = new();
        var acceptedPeers = new Dictionary<NetworkPeerId, ServerAccept>
        {
            [new NetworkPeerId(1)] = Accept(sessionId: 0, playerId: 10),
            [new NetworkPeerId(2)] = Accept(sessionId: 20, playerId: 20),
        };
        var sender = new ServerSnapshotSender(transport, acceptedPeers, (_, _) => null);

        int sent = sender.SendDueSnapshots(3);

        Assert.Equal(0, sent);
        Assert.Empty(transport.SentPackets);
    }

    private static ServerAccept Accept(ulong sessionId, uint playerId) => new(
        sessionId,
        ConnectionId: playerId + 100,
        playerId,
        ServerTick: 0,
        MapId: "graybox");

    private static ServerSnapshot SnapshotFor(ServerAccept accept) => new(
        ServerTick: 123,
        LocalPlayerId: accept.PlayerId,
        AcknowledgedInputSequence: 77,
        Players:
        [
            new PlayerSnapshotState(
                accept.PlayerId,
                ServerSnapshotPlayerKind.Human,
                new Vector3(1.0f, 2.0f, 3.0f),
                Vector3.Zero,
                YawRadians: 0.25f,
                PitchRadians: -0.5f,
                CurrentHealth: 80,
                MaxHealth: 100,
                Alive: true,
                new WeaponSnapshotState(
                    "rifle",
                    AmmoInMagazine: 29,
                    ReserveAmmo: 90,
                    NextAllowedFireTick: 130,
                    LastFiredTick: 120,
                    IsReloading: false,
                    ReloadCompleteTick: null)),
        ],
        Match: new MatchSnapshotState(
            ServerSnapshotMatchPhase.Playing,
            PhaseStartedTick: 60,
            LivingPlayerCount: 1,
            WinnerPlayerId: null),
        SafeZone: new SafeZoneSnapshotState(
            Vector3.Zero,
            CurrentRadius: 100.0f,
            TargetRadius: 50.0f,
            LastUpdatedTick: 90));

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
