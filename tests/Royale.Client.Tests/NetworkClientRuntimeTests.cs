using System.Numerics;
using Royale.Client.Networking;
using Royale.Client.Presentation;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Content;
using Royale.Network;
using Royale.Protocol;
using Royale.Simulation.Movement;

namespace Royale.Client.Tests;

public sealed class NetworkClientRuntimeTests
{
    [Fact]
    public void HandshakeClientIsCreatedOnlyAfterTransportConnected()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkClientRuntime(transport, new NetworkEndpoint("127.0.0.1", 7777));

        Assert.False(runtime.HandshakeStarted);
        Assert.Empty(transport.SentPackets);

        transport.QueueConnected(runtime.ServerPeerId);
        runtime.Poll();

        Assert.True(runtime.HandshakeStarted);
        SentPacket hello = Assert.Single(transport.SentPackets);
        Assert.Equal(ProtocolMessageType.ClientHello, ReadHeader(hello.Payload).MessageType);
    }

    [Fact]
    public void AcceptedHandshakeCreatesInputSenderAndFixedTickSendsInput()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkClientRuntime(transport, new NetworkEndpoint("127.0.0.1", 7777));
        ServerAccept accept = Accept();

        AcceptHandshake(runtime, transport, accept);
        transport.SentPackets.Clear();

        runtime.ApplyLook(new PlayerInputSample(
            Move: Vector2.Zero,
            Jump: false,
            Fire: false,
            LookDelta: new Vector2(10.0f, -20.0f)));

        Assert.True(runtime.FixedUpdate(
            new PlayerInputSample(
                Move: new Vector2(1.0f, 1.0f),
                Jump: true,
                Fire: true,
                LookDelta: Vector2.Zero),
            clientTick: 12));

        SentPacket inputPacket = Assert.Single(transport.SentPackets);
        Assert.Equal(NetworkDelivery.Unreliable, inputPacket.Delivery);
        Assert.Equal(ClientInputSender.InputChannel, inputPacket.Channel);
        Assert.True(ClientInputPayloadSerializer.TryReadCommands(
            ReadPayload(inputPacket.Payload),
            out PlayerInputCommand[] commands));
        PlayerInputCommand command = Assert.Single(commands);
        Assert.Equal(1U, command.Sequence);
        Assert.Equal(12U, command.ClientTick);
        Assert.InRange(command.Move.Length(), 0.999f, 1.001f);
        Assert.True(command.Buttons.HasFlag(InputButtons.Jump));
        Assert.True(command.Buttons.HasFlag(InputButtons.Fire));
        Assert.Equal(0.025f, command.YawRadians, precision: 4);
        Assert.Equal(0.05f, command.PitchRadians, precision: 4);
    }

    [Fact]
    public void SnapshotPacketsUpdateLatestNetworkState()
    {
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkClientRuntime(transport, new NetworkEndpoint("127.0.0.1", 7777));
        ServerAccept accept = Accept();
        ServerSnapshot snapshot = Snapshot(localPlayerId: accept.PlayerId);
        AcceptHandshake(runtime, transport, accept);

        runtime.PacketReceived(
            runtime.ServerPeerId,
            FrameSnapshot(accept.SessionId, snapshot),
            NetworkDelivery.Sequenced,
            ServerSnapshotSender.SnapshotChannel);

        Assert.NotNull(runtime.State.LatestSnapshot);
        Assert.Equal(snapshot.ServerTick, runtime.State.LatestSnapshot!.ServerTick);
        Assert.Equal(accept.PlayerId, runtime.State.LocalPlayerId);
        Assert.True(runtime.State.TryGetLocalPlayer(out PlayerSnapshotState localPlayer));
        Assert.Equal(accept.PlayerId, localPlayer.PlayerId);
    }

    [Fact]
    public void SnapshotPlayerStateBuildsFiniteDebugCapsules()
    {
        DebugPrimitiveList primitives = DebugSceneBuilder.Build(
            CreateMap(),
            localPlayer: null,
            Snapshot(localPlayerId: 1));

        Assert.True(primitives.LineCount > 0);
        Assert.All(primitives.Lines, line =>
        {
            AssertFinite(line.Start);
            AssertFinite(line.End);
        });
    }

    [Fact]
    public void LocalSnapshotPositionPlusLocalLookCreatesRenderCamera()
    {
        ClientNetworkState state = new();
        state.ApplySnapshot(Snapshot(localPlayerId: 1));

        RenderCamera camera = NetworkSnapshotPresentation.CreateRenderCamera(
            state,
            new PlayerLookState(YawRadians: 0.75f, PitchRadians: -0.25f),
            GameplayView.CreateDefault());

        Assert.True(float.IsFinite(camera.Position.X));
        Assert.True(float.IsFinite(camera.Position.Y));
        Assert.True(float.IsFinite(camera.Position.Z));
        Assert.Equal(0.75f, camera.YawRadians);
        Assert.Equal(-0.25f, camera.PitchRadians);
        Assert.True(camera.Position.Y > 1.0f);
    }

    private static void AcceptHandshake(
        NetworkClientRuntime runtime,
        FakeNetworkTransport transport,
        ServerAccept accept)
    {
        transport.QueueConnected(runtime.ServerPeerId);
        runtime.Poll();
        runtime.PacketReceived(
            runtime.ServerPeerId,
            FrameAccept(accept),
            NetworkDelivery.ReliableOrdered,
            channel: 0);
        Assert.True(runtime.Accepted);
    }

    private static ServerAccept Accept() => new(
        SessionId: 44,
        ConnectionId: 10,
        PlayerId: 20,
        ServerTick: 30,
        MapId: ContentCatalog.DefaultMapId);

    private static ServerSnapshot Snapshot(uint localPlayerId) => new(
        ServerTick: 123,
        LocalPlayerId: localPlayerId,
        AcknowledgedInputSequence: 77,
        Players:
        [
            new PlayerSnapshotState(
                localPlayerId,
                new Vector3(1.0f, 0.0f, 3.0f),
                Vector3.Zero,
                YawRadians: 0.25f,
                PitchRadians: -0.5f,
                CurrentHealth: 100,
                MaxHealth: 100,
                Alive: true,
                new WeaponSnapshotState(
                    "rifle",
                    AmmoInMagazine: 30,
                    ReserveAmmo: 90,
                    NextAllowedFireTick: 0,
                    LastFiredTick: null,
                    IsReloading: false,
                    ReloadCompleteTick: null)),
            new PlayerSnapshotState(
                99,
                new Vector3(3.0f, 0.0f, 1.0f),
                Vector3.Zero,
                YawRadians: 0.0f,
                PitchRadians: 0.0f,
                CurrentHealth: 100,
                MaxHealth: 100,
                Alive: true,
                new WeaponSnapshotState(
                    "rifle",
                    AmmoInMagazine: 30,
                    ReserveAmmo: 90,
                    NextAllowedFireTick: 0,
                    LastFiredTick: null,
                    IsReloading: false,
                    ReloadCompleteTick: null)),
        ],
        Match: new MatchSnapshotState(
            ServerSnapshotMatchPhase.InProgress,
            PhaseStartedTick: 60,
            LivingPlayerCount: 2,
            WinnerPlayerId: null),
        SafeZone: new SafeZoneSnapshotState(
            Vector3.Zero,
            CurrentRadius: 100.0f,
            TargetRadius: 50.0f,
            LastUpdatedTick: 90));

    private static byte[] FrameAccept(ServerAccept accept)
    {
        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxServerAcceptPayloadSize];
        Assert.True(HandshakePayloadSerializer.TryWriteServerAccept(accept, payload, out int bytesWritten));
        return FramePacket(ProtocolMessageType.ServerAccept, accept.SessionId, payload[..bytesWritten]);
    }

    private static byte[] FrameSnapshot(ulong sessionId, ServerSnapshot snapshot)
    {
        byte[] payload = new byte[ServerSnapshotPayloadSerializer.MaxServerSnapshotPayloadSize];
        Assert.True(ServerSnapshotPayloadSerializer.TryWriteSnapshot(snapshot, payload, out int bytesWritten));
        return FramePacket(ProtocolMessageType.ServerSnapshot, sessionId, payload.AsSpan(0, bytesWritten));
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

    private static byte[] ReadPayload(ReadOnlySpan<byte> packet)
    {
        Assert.True(ProtocolPacketFramer.TryReadPacket(
            packet,
            out _,
            out ReadOnlySpan<byte> payload,
            out ProtocolFrameError error));
        Assert.Equal(ProtocolFrameError.None, error);
        return payload.ToArray();
    }

    private static GameMap CreateMap() => new()
    {
        Id = "client-network-presentation",
        Name = "Client Network Presentation",
        SpawnPoints = [],
        StaticBoxes = [],
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, 0.0f),
            Radius = 20.0f,
        },
    };

    private static void AssertFinite(Vector3 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
    }

    private sealed class FakeNetworkTransport : INetworkTransport
    {
        private readonly Queue<Action<INetworkEventHandler>> events = [];

        public List<SentPacket> SentPackets { get; } = [];

        public void Start(int port)
        {
        }

        public NetworkPeerId Connect(NetworkEndpoint endpoint) => new(7);

        public void Send(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel = 0)
        {
            SentPackets.Add(new SentPacket(peerId, packet.ToArray(), delivery, channel));
        }

        public void Disconnect(NetworkPeerId peerId)
        {
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
    }

    private readonly record struct SentPacket(
        NetworkPeerId PeerId,
        byte[] Payload,
        NetworkDelivery Delivery,
        byte Channel);
}
