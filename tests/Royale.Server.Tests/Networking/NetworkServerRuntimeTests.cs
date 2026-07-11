using System.Numerics;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Server.Bots;
using Royale.Server.Launch;
using Royale.Server.Match;
using Royale.Server.Networking;
using Royale.Server.Observability;
using Royale.Server.Sessions;
using Royale.Server.Simulation;

using Royale.Server.Tests.Infrastructure;

namespace Royale.Server.Tests.Networking;

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

    [Fact]
    public void FullCountdownRejectsHandshakeWithoutCreatingPeerConnectionOrParticipant()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(
                ContentCatalog.DefaultMapId,
                new MatchStartSettings(minimumPlayers: 1, targetPlayers: 1)));
        _ = ConnectClient(runtime, transport, new NetworkPeerId(1));
        transport.SentPackets.Clear();

        runtime.Step();
        transport.SentPackets.Clear();
        NetworkPeerId rejectedPeer = new(2);
        transport.QueueConnected(rejectedPeer);
        transport.QueuePacket(rejectedPeer, FrameClientHello(), NetworkDelivery.ReliableOrdered, channel: 0);
        runtime.Step();

        SentPacket packet = Assert.Single(
            transport.SentPackets,
            sent => ReadHeader(sent.Payload).MessageType == ProtocolMessageType.ServerReject);
        ServerReject reject = ReadReject(packet.Payload);
        Assert.Equal(ServerRejectReason.MatchUnavailable, reject.Reason);
        Assert.Contains("full", reject.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Single(runtime.AcceptedPeers);
        Assert.Equal(1, runtime.ConnectedClientCount);
        Assert.Equal(1, runtime.ActivePlayerCount);
        Assert.DoesNotContain(rejectedPeer, runtime.AcceptedPeers.Keys);
    }

    [Fact]
    public void PlayingRejectsHandshakeWithLockedDetailAndNoAdmissionSideEffects()
    {
        FakeNetworkTransport transport = new();
        InProcessServerSession session = InProcessServerSession.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 1, targetPlayers: 2));
        using var runtime = new NetworkServerRuntime(
            transport,
            session);
        _ = ConnectClient(runtime, transport, new NetworkPeerId(1));
        runtime.Step();
        session.TransitionMatchPhase(MatchPhase.Playing);
        transport.SentPackets.Clear();

        NetworkPeerId rejectedPeer = new(2);
        transport.QueueConnected(rejectedPeer);
        transport.QueuePacket(rejectedPeer, FrameClientHello(), NetworkDelivery.ReliableOrdered, channel: 0);
        runtime.Step();

        SentPacket rejectPacket = Assert.Single(
            transport.SentPackets,
            sent => ReadHeader(sent.Payload).MessageType == ProtocolMessageType.ServerReject);
        ServerReject reject = ReadReject(rejectPacket.Payload);
        Assert.Equal(ServerRejectReason.MatchUnavailable, reject.Reason);
        Assert.Contains("locked", reject.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Single(runtime.AcceptedPeers);
        Assert.Equal(1, runtime.ConnectedClientCount);
        Assert.Equal(2, runtime.ActivePlayerCount);
        Assert.DoesNotContain(rejectedPeer, runtime.AcceptedPeers.Keys);
    }

    [Fact]
    public void ForceStartExposesAuthoritativeSessionResult()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId));

        Assert.Equal(ForceStartResult.NoPlayers, runtime.ForceStart());

        _ = ConnectClient(runtime, transport, new NetworkPeerId(1));

        Assert.Equal(ForceStartResult.Started, runtime.ForceStart());
        Assert.Equal(ForceStartResult.MatchNotWaiting, runtime.ForceStart());
    }

    [Fact]
    public void BotParticipantDoesNotCreatePeerAndIsReplicatedToConnectedHuman()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId));

        ServerPlayerId bot = runtime.AddBot();
        ServerAccept accept = ConnectClient(runtime, transport, new NetworkPeerId(1));

        Assert.Equal(1, runtime.ConnectedClientCount);
        Assert.Equal(1, runtime.HumanPlayerCount);
        Assert.Equal(1, runtime.BotPlayerCount);
        Assert.Equal(2, runtime.ActivePlayerCount);
        Assert.Single(runtime.AcceptedPeers);

        transport.SentPackets.Clear();
        runtime.Step();
        runtime.Step();

        SentPacket packet = Assert.Single(transport.SentPackets);
        ServerSnapshot snapshot = ReadSnapshot(packet.Payload);
        Assert.Equal(accept.PlayerId, snapshot.LocalPlayerId);
        Assert.Contains(snapshot.Players, player =>
            player.PlayerId == bot.Value && player.Kind == ServerSnapshotPlayerKind.Bot);
    }

    [Fact]
    public void RuntimeSubmitsBotInputThroughAuthoritativeSession()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(CreateOpenArenaMap()));
        ServerPlayerId bot = runtime.AddBot();
        ServerAccept accept = ConnectClient(runtime, transport, new NetworkPeerId(1));
        transport.SentPackets.Clear();

        Assert.True(runtime.TrySubmitBotInput(
            bot,
            new BotInputIntent(
                new Vector2(0.0f, 1.0f),
                MathF.PI / 2.0f,
                0.2f,
                InputButtons.None)));

        runtime.Step();
        runtime.Step();

        ServerSnapshot snapshot = ReadSnapshot(Assert.Single(transport.SentPackets).Payload);
        PlayerSnapshotState botState = Assert.Single(snapshot.Players, player => player.PlayerId == bot.Value);
        Assert.Equal(accept.PlayerId, snapshot.LocalPlayerId);
        Assert.True(botState.Position.X > 0.01f);
        Assert.Equal(1U, botState.LastProcessedInputSequence);
        Assert.Equal(1U, botState.LastProcessedInputClientTick);
    }

    [Fact]
    public void RuntimeAveragesUsableAcceptedHumanLatencyAndRoundsDelayUpToTicks()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(
                ContentCatalog.DefaultMapId,
                new MatchStartSettings(minimumPlayers: 4, targetPlayers: 4)));
        ServerPlayerId bot = runtime.AddBot();
        NetworkPeerId first = new(1);
        NetworkPeerId second = new(2);
        NetworkPeerId missing = new(3);
        _ = ConnectClient(runtime, transport, first);
        transport.SentPackets.Clear();
        _ = ConnectClient(runtime, transport, second);
        transport.SentPackets.Clear();
        _ = ConnectClient(runtime, transport, missing);
        transport.SentPackets.Clear();
        transport.SetLatency(first, 16);
        transport.SetLatency(second, 18);

        Assert.True(runtime.TrySubmitBotInput(bot, BotIntent()));

        Assert.Equal(new BotInputDelayDiagnostics(2, 17.0d, 2), runtime.BotInputDelayDiagnostics);
        Assert.Equal(1, runtime.QueuedInputCommandCount);
        runtime.Step();
        runtime.Step();
        Assert.Null(Assert.Single(
            runtime.GetPlayerDebugStates(), player => player.PlayerId == bot.Value).LastProcessedInputSequence);
        runtime.Step();
        Assert.Equal(1U, Assert.Single(
            runtime.GetPlayerDebugStates(), player => player.PlayerId == bot.Value).LastProcessedInputSequence);
    }

    [Fact]
    public void RuntimeUsesZeroBotDelayWithoutUsableConnectedHumanSamples()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId));
        ServerPlayerId firstBot = runtime.AddBot();
        ServerPlayerId secondBot = runtime.AddBot();
        NetworkPeerId peer = new(1);
        _ = ConnectClient(runtime, transport, peer);
        transport.SentPackets.Clear();
        transport.SetLatency(peer, -1);

        Assert.True(runtime.TrySubmitBotInput(firstBot, BotIntent()));
        Assert.Equal(default, runtime.BotInputDelayDiagnostics);

        transport.QueueDisconnected(peer);
        runtime.Step();

        Assert.True(runtime.TrySubmitBotInput(secondBot, BotIntent()));
        Assert.Equal(default, runtime.BotInputDelayDiagnostics);
        runtime.Step();
        Assert.Equal(1U, Assert.Single(
            runtime.GetPlayerDebugStates(), player => player.PlayerId == secondBot.Value).LastProcessedInputSequence);
    }

    private static ServerAccept ConnectClient(
        NetworkServerRuntime runtime,
        FakeNetworkTransport transport,
        NetworkPeerId peer)
    {
        transport.QueueConnected(peer);
        transport.QueuePacket(peer, FrameClientHello(), NetworkDelivery.ReliableOrdered, channel: 0);
        runtime.Step();
        return ReadAccept(Assert.Single(
            transport.SentPackets,
            packet => ReadHeader(packet.Payload).MessageType == ProtocolMessageType.ServerAccept).Payload);
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

    private static ServerReject ReadReject(ReadOnlySpan<byte> packet)
    {
        Assert.True(ProtocolPacketFramer.TryReadPacket(
            packet,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        Assert.Equal(ProtocolMessageType.ServerReject, header.MessageType);
        Assert.True(HandshakePayloadSerializer.TryReadServerReject(payload, out ServerReject? reject));
        return Assert.IsType<ServerReject>(reject);
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
            new MapSpawnPoint
            {
                Id = "spawn-b",
                Position = new MapVector3(0.0f, 0.0f, -10.0f),
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

    private static BotInputIntent BotIntent() => new(
        Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        InputButtons.None);

    private sealed class FakeNetworkTransport : INetworkTransport, INetworkTransportDiagnostics
    {
        private readonly Queue<Action<INetworkEventHandler>> events = [];
        private readonly Dictionary<NetworkPeerId, NetworkPeerStatistics> statistics = [];

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

        public void SetLatency(NetworkPeerId peerId, int oneWayLatencyMilliseconds)
        {
            statistics[peerId] = new NetworkPeerStatistics(
                oneWayLatencyMilliseconds,
                RoundTripTimeMilliseconds: oneWayLatencyMilliseconds * 2,
                MaximumTransmissionUnitBytes: 1200,
                TimeSinceLastPacketMilliseconds: 0.0f,
                PacketsSent: 0,
                PacketsReceived: 0,
                BytesSent: 0,
                BytesReceived: 0,
                PacketsLost: 0,
                PacketLossPercent: 0);
        }

        public bool TryGetPeerStatistics(NetworkPeerId peerId, out NetworkPeerStatistics peerStatistics) =>
            statistics.TryGetValue(peerId, out peerStatistics);

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
