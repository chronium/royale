using System.Diagnostics.Metrics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Royale.Content;
using Royale.Diagnostics;
using Royale.Network;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation.Combat;

namespace Royale.Server.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class ServerObservabilityTests
{
    [Fact]
    public void ConnectionAcceptAndDisconnectUpdateCountersAndGauges()
    {
        using MetricRecorder metrics = new(
            "royale.server.connections.accepted",
            "royale.server.connections.disconnected",
            "royale.server.players.connected",
            "royale.server.players.active",
            "royale.server.match.living_players",
            "royale.server.match.phase");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory);
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(1);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);

        ServerAccept accept = ConnectClient(runtime, transport, peer);
        metrics.CollectObservable();

        Assert.Equal(1, metrics.Sum("royale.server.connections.accepted"));
        Assert.Equal(1, metrics.Latest("royale.server.players.connected"));
        Assert.Equal(1, metrics.Latest("royale.server.players.active"));
        Assert.Equal(1, metrics.Latest("royale.server.match.living_players"));
        Assert.Equal(
            1,
            metrics.Latest(
                "royale.server.match.phase",
                tagKey: "phase",
                tagValue: "waiting_for_players"));
        Assert.Equal(1U, accept.ConnectionId);

        transport.QueueDisconnected(peer);
        runtime.Step();
        metrics.CollectObservable();

        MetricMeasurement disconnected = Assert.Single(
            metrics.MeasurementsFor("royale.server.connections.disconnected"));
        Assert.Equal(1, disconnected.Value);
        Assert.Equal("remote_connection_close", disconnected.Tags["reason"]);
        Assert.Equal(0, metrics.Latest("royale.server.players.connected"));
        Assert.Equal(0, metrics.Latest("royale.server.players.active"));
        Assert.Equal(0, metrics.Latest("royale.server.match.living_players"));
    }

    [Fact]
    public void ServerStepRecordsTickDuration()
    {
        using MetricRecorder metrics = new("royale.server.tick.duration");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory);
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);

        runtime.Step();

        MetricMeasurement tick = Assert.Single(metrics.MeasurementsFor("royale.server.tick.duration"));
        Assert.True(tick.Value >= 0.0d);
    }

    [Fact]
    public void InputQueueDepthGaugeReportsQueuedCommands()
    {
        using MetricRecorder metrics = new("royale.server.inputs.queue_depth");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory);
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection connection = session.ConnectClient();

        Assert.True(session.TryEnqueueInputCommand(connection, ValidCommand(sequence: 5)));
        observability.UpdateState(
            session.ConnectedClientCount,
            session.ActivePlayerCount,
            session.LivingPlayerCount,
            session.MatchPhase,
            session.QueuedInputCommandCount);
        metrics.CollectObservable();

        Assert.Equal(1, metrics.Latest("royale.server.inputs.queue_depth"));
    }

    [Theory]
    [InlineData(MatchPhase.WaitingForPlayers, "waiting_for_players")]
    [InlineData(MatchPhase.Countdown, "countdown")]
    [InlineData(MatchPhase.Playing, "playing")]
    [InlineData(MatchPhase.Finished, "finished")]
    [InlineData(MatchPhase.Resetting, "resetting")]
    public void MatchPhaseGaugeEmitsOneActiveStableLabel(MatchPhase phase, string expectedLabel)
    {
        using MetricRecorder metrics = new("royale.server.match.phase");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory);

        observability.UpdateState(0, 0, 0, phase, 0);
        metrics.CollectObservable();

        IReadOnlyList<MetricMeasurement> measurements = metrics.MeasurementsFor("royale.server.match.phase");
        Assert.Equal(5, measurements.Count);
        Assert.Equal(
            ["waiting_for_players", "playing", "finished", "countdown", "resetting"],
            measurements.Select(measurement => Assert.IsType<string>(measurement.Tags["phase"])).ToArray());
        Assert.Equal(1, metrics.Latest("royale.server.match.phase", "phase", expectedLabel));
        Assert.Equal(1, measurements.Count(measurement => measurement.Value == 1));
    }

    [Fact]
    public void MatchPhaseChangesProduceStructuredLogsWithStableNames()
    {
        CapturingLoggerProvider provider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });
        using ServerObservability observability = new(loggerFactory);

        observability.UpdateState(0, 0, 0, MatchPhase.WaitingForPlayers, 0);
        observability.UpdateState(0, 0, 0, MatchPhase.Countdown, 0);
        observability.UpdateState(0, 0, 0, MatchPhase.Playing, 0);
        observability.UpdateState(0, 0, 0, MatchPhase.Finished, 0);
        observability.UpdateState(0, 0, 0, MatchPhase.Resetting, 0);
        observability.UpdateState(0, 0, 0, MatchPhase.WaitingForPlayers, 0);

        LogEntry[] transitions = provider.Entries
            .Where(entry => entry.Message.StartsWith("Match phase changed", StringComparison.Ordinal))
            .ToArray();

        Assert.Collection(
            transitions,
            entry => AssertTransitionLog(entry, "waiting_for_players", "countdown"),
            entry => AssertTransitionLog(entry, "countdown", "playing"),
            entry => AssertTransitionLog(entry, "playing", "finished"),
            entry => AssertTransitionLog(entry, "finished", "resetting"),
            entry => AssertTransitionLog(entry, "resetting", "waiting_for_players"));
    }

    [Fact]
    public void SentSnapshotsIncrementCounter()
    {
        using MetricRecorder metrics = new("royale.server.snapshots.sent");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory);
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(1);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);

        _ = ConnectClient(runtime, transport, peer);
        runtime.Step();
        runtime.Step();

        Assert.Equal(1, metrics.Sum("royale.server.snapshots.sent"));
    }

    [Fact]
    public void ReceivedInputPacketsIncrementLowCardinalityPacketCounter()
    {
        using MetricRecorder metrics = new("royale.server.packets.received");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory);
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(1);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);
        ServerAccept accept = ConnectClient(runtime, transport, peer);
        metrics.Clear();

        transport.QueuePacket(
            peer,
            FrameInputPacket(accept.SessionId, ValidCommand(sequence: 5)),
            NetworkDelivery.Sequenced,
            ClientInputSender.InputChannel);
        runtime.Step();

        MetricMeasurement inputPacket = Assert.Single(
            metrics.MeasurementsFor("royale.server.packets.received"),
            measurement => measurement.Tags.TryGetValue("message_type", out object? value) &&
                Equals(value, "client_input"));
        Assert.Equal("sequenced", inputPacket.Tags["delivery"]);
        Assert.Equal(ClientInputSender.InputChannel, inputPacket.Tags["channel"]);
        Assert.DoesNotContain("peer", inputPacket.Tags.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("peer_id", inputPacket.Tags.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection_id", inputPacket.Tags.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("player_id", inputPacket.Tags.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandshakeRejectsAndMalformedInputIncrementInvalidCounters()
    {
        using MetricRecorder metrics = new(
            "royale.server.handshakes.rejected",
            "royale.server.packets.invalid");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory);
        FakeNetworkTransport transport = new();
        NetworkPeerId rejectedPeer = new(1);
        NetworkPeerId acceptedPeer = new(2);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);

        transport.QueueConnected(rejectedPeer);
        transport.QueuePacket(
            rejectedPeer,
            FrameClientHello(new ClientHello("wrong-build", ProtocolConstants.ContentVersion)),
            NetworkDelivery.ReliableOrdered,
            channel: 0);
        runtime.Step();

        ServerAccept accept = ConnectClient(runtime, transport, acceptedPeer);
        transport.QueuePacket(
            acceptedPeer,
            FramePacket(ProtocolMessageType.ClientInput, accept.SessionId, [0]),
            NetworkDelivery.Sequenced,
            ClientInputSender.InputChannel);
        runtime.Step();

        Assert.Equal(
            1,
            metrics.Sum(
                "royale.server.handshakes.rejected",
                tagKey: "reason",
                tagValue: "incompatible_build"));
        Assert.Equal(
            1,
            metrics.Sum(
                "royale.server.packets.invalid",
                tagKey: "reason",
                tagValue: "handshake_incompatible_build"));
        Assert.Equal(
            1,
            metrics.Sum(
                "royale.server.packets.invalid",
                tagKey: "reason",
                tagValue: "malformed_payload"));
    }

    [Fact]
    public void PlayerDebugLogsEmitAtBoundedCadenceWithStructuredState()
    {
        CapturingLoggerProvider provider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });
        using ServerObservability observability = new(loggerFactory, playerDebugLogIntervalTicks: 3);
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(7);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(CreateOpenArenaMap()),
            observability);
        ServerAccept accept = ConnectClient(runtime, transport, peer);
        provider.Entries.Clear();

        transport.QueuePacket(
            peer,
            FrameInputPacket(accept.SessionId, ValidCommand(sequence: 17) with
            {
                Move = new Vector2(0.0f, 1.0f),
                YawRadians = MathF.PI / 2.0f,
                PitchRadians = 0.25f,
            }),
            NetworkDelivery.Sequenced,
            ClientInputSender.InputChannel);
        runtime.Step();
        Assert.DoesNotContain(
            provider.Entries,
            entry => entry.Message.StartsWith("Authoritative player debug state:", StringComparison.Ordinal));

        runtime.Step();

        LogEntry playerDebug = Assert.Single(
            provider.Entries,
            entry => entry.Message.StartsWith("Authoritative player debug state:", StringComparison.Ordinal));
        Assert.Equal(3UL, playerDebug.Properties["ServerTick"]);
        Assert.Equal(7, playerDebug.Properties["PeerId"]);
        Assert.Equal(accept.ConnectionId, playerDebug.Properties["ConnectionId"]);
        Assert.Equal(accept.PlayerId, playerDebug.Properties["PlayerId"]);
        Assert.Equal(MathF.PI / 2.0f, playerDebug.Properties["YawRadians"]);
        Assert.Equal(0.25f, playerDebug.Properties["PitchRadians"]);
        Assert.Equal(HealthState.DefaultPlayer.CurrentHealth, playerDebug.Properties["CurrentHealth"]);
        Assert.Equal(HealthState.DefaultPlayer.MaxHealth, playerDebug.Properties["MaxHealth"]);
        Assert.Equal(true, playerDebug.Properties["Alive"]);
        Assert.Equal(WeaponCatalog.DefaultRifle.Id, playerDebug.Properties["WeaponId"]);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize, playerDebug.Properties["AmmoInMagazine"]);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize * 3, playerDebug.Properties["ReserveAmmo"]);
        Assert.Equal(false, playerDebug.Properties["IsReloading"]);
        Assert.Equal(17U, playerDebug.Properties["LastProcessedInputSequence"]);
        Assert.Equal(117U, playerDebug.Properties["LastProcessedInputClientTick"]);
        Assert.Equal(0, playerDebug.Properties["QueuedInputCount"]);
        Assert.True((float)playerDebug.Properties["PositionX"]! > 0.01f);
        Assert.True(playerDebug.Properties.ContainsKey("VelocityX"));
    }

    [Fact]
    public void PlayerDebugLogsSkipWhenNoPlayers()
    {
        CapturingLoggerProvider provider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });
        using ServerObservability observability = new(loggerFactory, playerDebugLogIntervalTicks: 1);
        FakeNetworkTransport transport = new();
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);

        runtime.Step();
        runtime.Step();

        Assert.DoesNotContain(
            provider.Entries,
            entry => entry.Message.StartsWith("Authoritative player debug state:", StringComparison.Ordinal));
    }

    [Fact]
    public void PlayerDebugLogsDoNotIntroducePerPlayerMetricLabels()
    {
        using MetricRecorder metrics = new(
            "royale.server.connections.accepted",
            "royale.server.players.connected",
            "royale.server.players.active",
            "royale.server.match.living_players",
            "royale.server.match.phase",
            "royale.server.inputs.queue_depth",
            "royale.server.tick.duration",
            "royale.server.snapshots.sent",
            "royale.server.packets.received",
            "royale.server.packets.invalid",
            "royale.server.connections.disconnected",
            "royale.server.handshakes.rejected");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
        using ServerObservability observability = new(loggerFactory, playerDebugLogIntervalTicks: 1);
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(3);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);
        ServerAccept accept = ConnectClient(runtime, transport, peer);

        transport.QueuePacket(
            peer,
            FrameInputPacket(accept.SessionId, ValidCommand(sequence: 5)),
            NetworkDelivery.Sequenced,
            ClientInputSender.InputChannel);
        runtime.Step();
        metrics.CollectObservable();

        string[] forbiddenMetricLabels =
        [
            "peer",
            "peer_id",
            "connection",
            "connection_id",
            "player",
            "player_id",
            "endpoint",
            "position",
            "health",
            "ammo",
        ];
        foreach (MetricMeasurement measurement in metrics.Measurements)
        {
            foreach (string label in forbiddenMetricLabels)
            {
                Assert.DoesNotContain(
                    measurement.Tags.Keys,
                    key => string.Equals(key, label, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void StructuredLogsIncludeConnectionIdentifiersWhereAvailable()
    {
        CapturingLoggerProvider provider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });
        using ServerObservability observability = new(loggerFactory);
        FakeNetworkTransport transport = new();
        NetworkPeerId peer = new(1);
        NetworkPeerId rejectedPeer = new(9);
        using var runtime = new NetworkServerRuntime(
            transport,
            InProcessServerSession.Create(ContentCatalog.DefaultMapId),
            observability);

        transport.QueueConnected(rejectedPeer);
        transport.QueuePacket(
            rejectedPeer,
            FrameClientHello(new ClientHello("wrong-build", ProtocolConstants.ContentVersion)),
            NetworkDelivery.ReliableOrdered,
            channel: 0);
        runtime.Step();
        _ = ConnectClient(runtime, transport, peer);
        transport.QueueDisconnected(peer);
        runtime.Step();

        LogEntry rejected = Assert.Single(
            provider.Entries,
            entry => entry.Message.StartsWith("Handshake rejected:", StringComparison.Ordinal));
        Assert.Equal(9, rejected.Properties["PeerId"]);
        Assert.Equal("incompatible_build", rejected.Properties["Reason"]);

        LogEntry accepted = Assert.Single(
            provider.Entries,
            entry => entry.Message.StartsWith("Client accepted:", StringComparison.Ordinal));
        Assert.Equal(1, accepted.Properties["PeerId"]);
        Assert.Equal(1U, accepted.Properties["ConnectionId"]);
        Assert.Equal(1U, accepted.Properties["PlayerId"]);

        LogEntry disconnected = Assert.Single(
            provider.Entries,
            entry => entry.Message.StartsWith("Peer disconnected:", StringComparison.Ordinal));
        Assert.Equal(1, disconnected.Properties["PeerId"]);
        Assert.Equal(1U, disconnected.Properties["ConnectionId"]);
        Assert.Equal(1U, disconnected.Properties["PlayerId"]);
        Assert.Equal("remote_connection_close", disconnected.Properties["Reason"]);
    }

    private static ServerAccept ConnectClient(
        NetworkServerRuntime runtime,
        FakeNetworkTransport transport,
        NetworkPeerId peer)
    {
        transport.QueueConnected(peer);
        transport.QueuePacket(
            peer,
            FrameClientHello(new ClientHello(ProtocolConstants.BuildId, ProtocolConstants.ContentVersion)),
            NetworkDelivery.ReliableOrdered,
            channel: 0);
        runtime.Step();
        return ReadAccept(Assert.Single(transport.SentPackets, packet => ReadHeader(packet.Payload).MessageType == ProtocolMessageType.ServerAccept).Payload);
    }

    private static void AssertTransitionLog(LogEntry entry, string previous, string current)
    {
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(previous, entry.Properties["PreviousPhase"]);
        Assert.Equal(current, entry.Properties["CurrentPhase"]);
    }

    private static byte[] FrameClientHello(ClientHello hello)
    {
        Span<byte> payload = stackalloc byte[HandshakePayloadSerializer.MaxClientHelloPayloadSize];
        Assert.True(HandshakePayloadSerializer.TryWriteClientHello(hello, payload, out int bytesWritten));
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

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);

    private static GameMap CreateOpenArenaMap() => new()
    {
        Id = "observability-open-arena",
        Name = "Observability Open Arena",
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
                Size = new MapVector3(30.0f, 0.2f, 30.0f),
            },
        ],
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, 0.0f),
            Radius = 50.0f,
        },
    };

    private sealed class MetricRecorder : IDisposable
    {
        private readonly HashSet<string> instrumentNames;
        private readonly MeterListener listener = new();

        public MetricRecorder(params string[] instrumentNames)
        {
            this.instrumentNames = new HashSet<string>(instrumentNames, StringComparer.Ordinal);
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == RoyaleTelemetry.ServerMeterName &&
                    this.instrumentNames.Contains(instrument.Name))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<int>(Record);
            listener.SetMeasurementEventCallback<long>(Record);
            listener.SetMeasurementEventCallback<double>(Record);
            listener.Start();
        }

        public List<MetricMeasurement> Measurements { get; } = [];

        public void CollectObservable()
        {
            listener.RecordObservableInstruments();
        }

        public void Clear()
        {
            Measurements.Clear();
        }

        public IReadOnlyList<MetricMeasurement> MeasurementsFor(string name) =>
            Measurements.Where(measurement => measurement.Name == name).ToArray();

        public double Sum(string name, string? tagKey = null, object? tagValue = null) =>
            Measurements
                .Where(measurement => measurement.Name == name && MatchesTag(measurement, tagKey, tagValue))
                .Sum(measurement => measurement.Value);

        public double Latest(string name, string? tagKey = null, object? tagValue = null) =>
            Measurements
                .Where(measurement => measurement.Name == name && MatchesTag(measurement, tagKey, tagValue))
                .Last()
                .Value;

        public void Dispose()
        {
            listener.Dispose();
        }

        private void Record<T>(
            Instrument instrument,
            T measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
            where T : struct
        {
            Measurements.Add(new MetricMeasurement(
                instrument.Name,
                Convert.ToDouble(measurement, System.Globalization.CultureInfo.InvariantCulture),
                CopyTags(tags)));
        }

        private static bool MatchesTag(
            MetricMeasurement measurement,
            string? tagKey,
            object? tagValue)
        {
            if (tagKey is null)
                return true;

            return measurement.Tags.TryGetValue(tagKey, out object? value) && Equals(value, tagValue);
        }

        private static Dictionary<string, object?> CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            Dictionary<string, object?> copy = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
                copy[tag.Key] = tag.Value;

            return copy;
        }
    }

    private sealed record MetricMeasurement(
        string Name,
        double Value,
        Dictionary<string, object?> Tags);

    private sealed class FakeNetworkTransport : INetworkTransport
    {
        private readonly Queue<Action<INetworkEventHandler>> events = [];

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

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, object?> properties = new(StringComparer.Ordinal);
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (KeyValuePair<string, object?> value in values)
                    properties[value.Key] = value.Value;
            }

            entries.Add(new LogEntry(logLevel, formatter(state, exception), properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Dictionary<string, object?> Properties);
}
