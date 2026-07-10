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

[Collection(Box3DNativeTestCollection.Name)]
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
        Assert.Equal(NetworkDelivery.Sequenced, inputPacket.Delivery);
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
        Assert.Equal(1, runtime.RemoteSnapshotBufferCount);
        Assert.Equal(RemoteSnapshotInterpolator.DefaultInterpolationDelayTicks, runtime.RemoteInterpolationDelayTicks);
    }

    [Fact]
    public void PredictionDoesNotRunBeforeAcceptedSnapshotSeed()
    {
        FakeNetworkTransport transport = new();
        GameMap map = CreatePredictionMap("prediction-handshake");
        using var runtime = new NetworkClientRuntime(
            transport,
            new NetworkEndpoint("127.0.0.1", 7777),
            loadPredictionMap: LoadMap(map));
        ServerAccept accept = Accept(map.Id);

        Assert.False(runtime.FixedUpdate(
            new PlayerInputSample(Vector2.UnitY, Jump: false, Fire: false, LookDelta: Vector2.Zero),
            clientTick: 1));
        Assert.False(runtime.PredictionMapAvailable);
        Assert.False(runtime.PredictionSeeded);
        Assert.False(runtime.PredictionActive);
        Assert.Equal(0, runtime.PendingInputCount);
        Assert.False(runtime.TryGetPredictedLocalPlayer(out _));

        AcceptHandshake(runtime, transport, accept);

        Assert.True(runtime.PredictionMapAvailable);
        Assert.False(runtime.PredictionSeeded);
        Assert.False(runtime.PredictionActive);

        Assert.True(runtime.FixedUpdate(
            new PlayerInputSample(Vector2.UnitY, Jump: false, Fire: false, LookDelta: Vector2.Zero),
            clientTick: 2));

        Assert.Equal(1, runtime.PendingInputCount);
        Assert.False(runtime.PredictionSeeded);
        Assert.False(runtime.PredictionActive);
        Assert.False(runtime.TryGetPredictedLocalPlayer(out _));
    }

    [Fact]
    public void FixedUpdatePredictsLocalMovementWithoutMutatingLatestSnapshot()
    {
        FakeNetworkTransport transport = new();
        GameMap map = CreatePredictionMap("prediction-movement");
        using var runtime = new NetworkClientRuntime(
            transport,
            new NetworkEndpoint("127.0.0.1", 7777),
            loadPredictionMap: LoadMap(map));
        ServerAccept accept = Accept(map.Id);
        AcceptHandshake(runtime, transport, accept);
        ServerSnapshot seed = Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: Vector3.Zero);
        ReceiveSnapshot(runtime, accept, seed);
        ServerSnapshot authoritativeSnapshot = runtime.State.LatestSnapshot!;
        Assert.True(runtime.State.TryGetLocalPlayer(out PlayerSnapshotState authoritativePlayer));

        Assert.True(runtime.FixedUpdate(
            new PlayerInputSample(Vector2.UnitY, Jump: false, Fire: false, LookDelta: Vector2.Zero),
            clientTick: 1));

        Assert.True(runtime.TryGetPredictedLocalPlayer(out PlayerSnapshotState predictedPlayer));
        Assert.True(Vector3.Distance(predictedPlayer.Position, authoritativePlayer.Position) > 0.001f);
        Assert.Same(authoritativeSnapshot, runtime.State.LatestSnapshot);
        Assert.True(runtime.State.TryGetLocalPlayer(out PlayerSnapshotState statePlayer));
        Assert.Equal(authoritativePlayer.Position, statePlayer.Position);
    }

    [Fact]
    public void SnapshotAcknowledgementsDropPendingInputsAndResyncWhenEmpty()
    {
        FakeNetworkTransport transport = new();
        GameMap map = CreatePredictionMap("prediction-ack-empty");
        using var runtime = new NetworkClientRuntime(
            transport,
            new NetworkEndpoint("127.0.0.1", 7777),
            loadPredictionMap: LoadMap(map));
        ServerAccept accept = Accept(map.Id);
        AcceptHandshake(runtime, transport, accept);
        ReceiveSnapshot(runtime, accept, Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: Vector3.Zero));

        Assert.True(runtime.FixedUpdate(
            new PlayerInputSample(Vector2.UnitY, Jump: false, Fire: false, LookDelta: Vector2.Zero),
            clientTick: 1));
        Assert.Equal(1, runtime.PendingInputCount);

        Vector3 authoritativePosition = new(2.0f, 0.0f, -2.0f);
        ReceiveSnapshot(runtime, accept, Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: 1,
            localPosition: authoritativePosition));

        Assert.Equal(0, runtime.PendingInputCount);
        Assert.True(runtime.TryGetPredictedLocalPlayer(out PlayerSnapshotState predictedPlayer));
        Assert.Equal(authoritativePosition, predictedPlayer.Position);
        Assert.Equal(0, runtime.LastReplayedInputCount);
    }

    [Fact]
    public void SnapshotAcknowledgementsReplayUnacknowledgedPredictionFromAuthoritativeBase()
    {
        FakeNetworkTransport transport = new();
        GameMap map = CreatePredictionMap("prediction-ack-partial");
        using var runtime = new NetworkClientRuntime(
            transport,
            new NetworkEndpoint("127.0.0.1", 7777),
            loadPredictionMap: LoadMap(map));
        ServerAccept accept = Accept(map.Id);
        AcceptHandshake(runtime, transport, accept);
        ReceiveSnapshot(runtime, accept, Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: Vector3.Zero));

        var moveForward = new PlayerInputSample(Vector2.UnitY, Jump: false, Fire: false, LookDelta: Vector2.Zero);
        Assert.True(runtime.FixedUpdate(moveForward, clientTick: 1));
        Assert.True(runtime.FixedUpdate(moveForward, clientTick: 2));

        Vector3 authoritativePosition = new(3.0f, 0.0f, 3.0f);
        ServerSnapshot correctionSnapshot = Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: 1,
            localPosition: authoritativePosition);
        transport.SentPackets.Clear();

        ReceiveSnapshot(runtime, accept, correctionSnapshot);

        Assert.Equal(1, runtime.PendingInputCount);
        Assert.Equal(1, runtime.LastReplayedInputCount);
        Assert.True(runtime.LastPredictionCorrectionDistance > 0.001f);
        Assert.True(runtime.ReconciliationCount >= 2);
        Assert.Empty(transport.SentPackets);
        Assert.NotNull(runtime.State.LatestSnapshot);
        ServerSnapshot latestSnapshot = runtime.State.LatestSnapshot!;
        Assert.Equal(correctionSnapshot.AcknowledgedInputSequence, latestSnapshot.AcknowledgedInputSequence);
        Assert.Equal(authoritativePosition, latestSnapshot.Players[0].Position);
        Assert.True(runtime.TryGetPredictedLocalPlayer(out PlayerSnapshotState afterSnapshot));
        Assert.True(afterSnapshot.Position.Z < authoritativePosition.Z - 0.05f);
        Assert.NotEqual(authoritativePosition, afterSnapshot.Position);
    }

    [Fact]
    public void ReconciliationDoesNotMutateLatestSnapshot()
    {
        FakeNetworkTransport transport = new();
        GameMap map = CreatePredictionMap("prediction-authoritative-state");
        using var runtime = new NetworkClientRuntime(
            transport,
            new NetworkEndpoint("127.0.0.1", 7777),
            loadPredictionMap: LoadMap(map));
        ServerAccept accept = Accept(map.Id);
        AcceptHandshake(runtime, transport, accept);
        ReceiveSnapshot(runtime, accept, Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: Vector3.Zero));

        Assert.True(runtime.FixedUpdate(
            new PlayerInputSample(Vector2.UnitY, Jump: false, Fire: false, LookDelta: Vector2.Zero),
            clientTick: 1));

        Vector3 authoritativePosition = new(2.0f, 0.0f, 2.0f);
        ServerSnapshot correctionSnapshot = Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: authoritativePosition);
        ReceiveSnapshot(runtime, accept, correctionSnapshot);

        Assert.NotNull(runtime.State.LatestSnapshot);
        ServerSnapshot latestSnapshot = runtime.State.LatestSnapshot!;
        Assert.Equal(correctionSnapshot.AcknowledgedInputSequence, latestSnapshot.AcknowledgedInputSequence);
        Assert.Equal(authoritativePosition, latestSnapshot.Players[0].Position);
        Assert.True(runtime.TryGetPredictedLocalPlayer(out PlayerSnapshotState predictedPlayer));
        Assert.NotEqual(latestSnapshot.Players[0].Position, predictedPlayer.Position);
    }

    [Fact]
    public void DeadAuthoritativeLocalPlayerDoesNotReplayPendingMovement()
    {
        FakeNetworkTransport transport = new();
        GameMap map = CreatePredictionMap("prediction-dead-local");
        using var runtime = new NetworkClientRuntime(
            transport,
            new NetworkEndpoint("127.0.0.1", 7777),
            loadPredictionMap: LoadMap(map));
        ServerAccept accept = Accept(map.Id);
        AcceptHandshake(runtime, transport, accept);
        ReceiveSnapshot(runtime, accept, Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: Vector3.Zero));

        Assert.True(runtime.FixedUpdate(
            new PlayerInputSample(Vector2.UnitY, Jump: false, Fire: false, LookDelta: Vector2.Zero),
            clientTick: 1));

        Vector3 deadPosition = new(6.0f, 0.0f, -4.0f);
        ReceiveSnapshot(runtime, accept, Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: deadPosition,
            localAlive: false));

        Assert.Equal(1, runtime.PendingInputCount);
        Assert.Equal(0, runtime.LastReplayedInputCount);
        Assert.True(runtime.TryGetPredictedLocalPlayer(out PlayerSnapshotState predictedPlayer));
        Assert.Equal(deadPosition, predictedPlayer.Position);
        Assert.False(predictedPlayer.Alive);
    }

    [Fact]
    public void DisposeReleasesPredictionAndRuntimeRejectsFurtherUse()
    {
        FakeNetworkTransport transport = new();
        GameMap map = CreatePredictionMap("prediction-dispose");
        var runtime = new NetworkClientRuntime(
            transport,
            new NetworkEndpoint("127.0.0.1", 7777),
            loadPredictionMap: LoadMap(map));
        ServerAccept accept = Accept(map.Id);
        AcceptHandshake(runtime, transport, accept);
        ReceiveSnapshot(runtime, accept, Snapshot(
            localPlayerId: accept.PlayerId,
            acknowledgedInputSequence: null,
            localPosition: Vector3.Zero));
        Assert.True(runtime.PredictionActive);

        runtime.Dispose();

        Assert.True(transport.Disposed);
        Assert.Throws<ObjectDisposedException>(() => runtime.Poll());
        Assert.Throws<ObjectDisposedException>(() => runtime.TryGetPredictedLocalPlayer(out _));
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

    [Fact]
    public void PresentationSnapshotSubstitutesPredictedLocalPlayerOnly()
    {
        ClientNetworkState state = new();
        state.ApplySnapshot(Snapshot(localPlayerId: 1, localPosition: Vector3.Zero, remotePosition: new Vector3(9.0f, 0.0f, 9.0f)));
        PlayerSnapshotState predicted = state.LatestSnapshot!.Players[0] with
        {
            Position = new Vector3(5.0f, 0.0f, -2.0f),
        };

        ServerSnapshot presentationSnapshot = NetworkSnapshotPresentation.CreatePresentationSnapshot(state, predicted)!;

        Assert.NotSame(state.LatestSnapshot, presentationSnapshot);
        Assert.Equal(Vector3.Zero, state.LatestSnapshot.Players[0].Position);
        Assert.Equal(predicted.Position, presentationSnapshot.Players[0].Position);
        Assert.Equal(state.LatestSnapshot.Players[1], presentationSnapshot.Players[1]);
    }

    [Fact]
    public void PresentationCameraUsesPredictedLocalPlayerWhenAvailable()
    {
        ClientNetworkState state = new();
        state.ApplySnapshot(Snapshot(localPlayerId: 1, localPosition: Vector3.Zero));
        PlayerSnapshotState predicted = state.LatestSnapshot!.Players[0] with
        {
            Position = new Vector3(4.0f, 0.0f, -4.0f),
        };

        RenderCamera camera = NetworkSnapshotPresentation.CreateRenderCamera(
            state,
            new PlayerLookState(YawRadians: 0.75f, PitchRadians: -0.25f),
            GameplayView.CreateDefault(),
            predicted);

        Assert.Equal(4.0f, camera.Position.X, precision: 4);
        Assert.Equal(-4.0f, camera.Position.Z, precision: 4);
        Assert.Equal(Vector3.Zero, state.LatestSnapshot.Players[0].Position);
    }

    [Fact]
    public void PredictedPresentationSnapshotBuildsLocalDebugCapsuleAtPredictedPosition()
    {
        ClientNetworkState state = new();
        state.ApplySnapshot(Snapshot(localPlayerId: 1, localPosition: Vector3.Zero, remotePosition: new Vector3(8.0f, 0.0f, 8.0f)));
        PlayerSnapshotState predicted = state.LatestSnapshot!.Players[0] with
        {
            Position = new Vector3(5.0f, 0.0f, -2.0f),
        };
        ServerSnapshot presentationSnapshot = NetworkSnapshotPresentation.CreatePresentationSnapshot(state, predicted)!;

        DebugPrimitiveList primitives = DebugSceneBuilder.Build(
            CreateMap(),
            localPlayer: null,
            presentationSnapshot);

        var localColor = new Vector4(0.20f, 0.72f, 1.0f, 1.0f);
        Assert.Contains(primitives.Lines, line =>
            line.Color == localColor &&
            MathF.Abs(line.Start.X - 5.0f) < 0.6f &&
            MathF.Abs(line.Start.Z - -2.0f) < 0.6f);
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

    private static ServerAccept Accept(string? mapId = null) => new(
        SessionId: 44,
        ConnectionId: 10,
        PlayerId: 20,
        ServerTick: 30,
        MapId: mapId ?? ContentCatalog.DefaultMapId);

    private static ServerSnapshot Snapshot(
        uint localPlayerId,
        uint? acknowledgedInputSequence = 77,
        Vector3? localPosition = null,
        Vector3? remotePosition = null,
        bool localAlive = true) => new(
        ServerTick: 123,
        LocalPlayerId: localPlayerId,
        AcknowledgedInputSequence: acknowledgedInputSequence,
        Players:
        [
            new PlayerSnapshotState(
                localPlayerId,
                localPosition ?? new Vector3(1.0f, 0.0f, 3.0f),
                Vector3.Zero,
                YawRadians: 0.25f,
                PitchRadians: -0.5f,
                CurrentHealth: localAlive ? 100 : 0,
                MaxHealth: 100,
                Alive: localAlive,
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
                remotePosition ?? new Vector3(3.0f, 0.0f, 1.0f),
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
            ServerSnapshotMatchPhase.Playing,
            PhaseStartedTick: 60,
            LivingPlayerCount: 2,
            WinnerPlayerId: null),
        SafeZone: new SafeZoneSnapshotState(
            Vector3.Zero,
            CurrentRadius: 100.0f,
            TargetRadius: 50.0f,
            LastUpdatedTick: 90));

    private static void ReceiveSnapshot(
        NetworkClientRuntime runtime,
        ServerAccept accept,
        ServerSnapshot snapshot)
    {
        runtime.PacketReceived(
            runtime.ServerPeerId,
            FrameSnapshot(accept.SessionId, snapshot),
            NetworkDelivery.Sequenced,
            ServerSnapshotSender.SnapshotChannel);
    }

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

    private static GameMap CreatePredictionMap(string mapId) => new()
    {
        Id = mapId,
        Name = "Prediction Test",
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
                Size = new MapVector3(40.0f, 0.2f, 40.0f),
            },
        ],
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, 0.0f),
            Radius = 20.0f,
        },
    };

    private static Func<string, GameMap> LoadMap(GameMap map) =>
        requestedMapId =>
        {
            Assert.Equal(map.Id, requestedMapId);
            return map;
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

        public bool Disposed { get; private set; }

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
            Disposed = true;
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
