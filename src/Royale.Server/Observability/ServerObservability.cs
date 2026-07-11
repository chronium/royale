using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Royale.Diagnostics.Logging;
using Royale.Diagnostics.Telemetry;
using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Server.Match;
using Royale.Server.Networking;
using Royale.Server.Sessions;
using Royale.Server.Simulation;
using Royale.Simulation.World;

namespace Royale.Server.Observability;

public sealed class ServerObservability : IDisposable
{
    public const int DefaultPlayerDebugLogIntervalTicks = SimulationSettings.TickRateHz;

    private static readonly object InstancesLock = new();
    private static readonly MatchPhase[] MatchPhases = Enum.GetValues<MatchPhase>();
    private static readonly List<ServerObservability> Instances = [];
    private static readonly ObservableGauge<int> ConnectedPlayersGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.players.connected",
            ObserveConnectedPlayers,
            description: "Accepted server client connections.");
    private static readonly ObservableGauge<int> ActivePlayersGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.players.active",
            ObserveActivePlayers,
            description: "Authoritative active players.");
    private static readonly ObservableGauge<int> BotPlayersGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.players.bots",
            ObserveBotPlayers,
            description: "Authoritative bot participants.");
    private static readonly ObservableGauge<int> LivingPlayersGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.match.living_players",
            ObserveLivingPlayers,
            description: "Living players in the authoritative match state.");
    private static readonly ObservableGauge<int> MatchPhaseGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.match.phase",
            ObserveMatchPhase,
            description: "Current authoritative match phase, emitted as one active phase label.");
    private static readonly ObservableGauge<int> InputQueueDepthGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.inputs.queue_depth",
            ObserveInputQueueDepth,
            description: "Queued input commands waiting for authoritative simulation.");
    private static readonly ObservableGauge<double> BotInputLatencyGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.bots.input_delay.latency",
            ObserveBotInputLatency,
            unit: "ms",
            description: "Average sampled one-way latency used to schedule newly generated bot input.");
    private static readonly ObservableGauge<int> BotInputDelayTicksGauge =
        RoyaleTelemetry.ServerMeter.CreateObservableGauge(
            "royale.server.bots.input_delay.ticks",
            ObserveBotInputDelayTicks,
            unit: "ticks",
            description: "Effective delay ticks used to schedule newly generated bot input.");
    private static readonly Histogram<double> TickDuration =
        RoyaleTelemetry.ServerMeter.CreateHistogram<double>(
            "royale.server.tick.duration",
            unit: "ms",
            description: "Dedicated server runtime step duration.");
    private static readonly Counter<long> SnapshotsSentCounter =
        RoyaleTelemetry.ServerMeter.CreateCounter<long>(
            "royale.server.snapshots.sent",
            description: "Server snapshots sent to clients.");
    private static readonly Counter<long> PacketsReceived =
        RoyaleTelemetry.ServerMeter.CreateCounter<long>(
            "royale.server.packets.received",
            description: "Packets received by the server runtime.");
    private static readonly Counter<long> PacketsInvalid =
        RoyaleTelemetry.ServerMeter.CreateCounter<long>(
            "royale.server.packets.invalid",
            description: "Invalid packets or rejected input packets observed by the server runtime.");
    private static readonly Counter<long> ConnectionsAccepted =
        RoyaleTelemetry.ServerMeter.CreateCounter<long>(
            "royale.server.connections.accepted",
            description: "Client connections accepted by the authoritative server.");
    private static readonly Counter<long> ConnectionsDisconnected =
        RoyaleTelemetry.ServerMeter.CreateCounter<long>(
            "royale.server.connections.disconnected",
            description: "Accepted client connections disconnected from the authoritative server.");
    private static readonly Counter<long> HandshakesRejected =
        RoyaleTelemetry.ServerMeter.CreateCounter<long>(
            "royale.server.handshakes.rejected",
            description: "Handshake attempts rejected by the server.");

    private readonly ILogger logger;
    private readonly int playerDebugLogIntervalTicks;
    private int connectedPlayers;
    private int activePlayers;
    private int botPlayers;
    private int livingPlayers;
    private int queuedInputCommands;
    private double botInputLatencyMilliseconds;
    private int botInputDelayTicks;
    private BotInputDelayDiagnostics? lastLoggedBotInputDelay;
    private MatchPhase matchPhase;
    private MatchPhase? lastObservedMatchPhase;
    private bool disposed;

    public ServerObservability(
        ILoggerFactory loggerFactory,
        int playerDebugLogIntervalTicks = DefaultPlayerDebugLogIntervalTicks)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        if (playerDebugLogIntervalTicks <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(playerDebugLogIntervalTicks),
                playerDebugLogIntervalTicks,
                "Player debug log interval must be positive.");

        logger = loggerFactory.CreateLogger("Royale.Server.Observability");
        this.playerDebugLogIntervalTicks = playerDebugLogIntervalTicks;

        lock (InstancesLock)
            Instances.Add(this);
    }

    public void UpdateState(
        int connectedPlayers,
        int activePlayers,
        int livingPlayers,
        MatchPhase matchPhase,
        int queuedInputCommands,
        int botPlayers = 0)
    {
        this.connectedPlayers = connectedPlayers;
        this.activePlayers = activePlayers;
        this.botPlayers = botPlayers;
        this.livingPlayers = livingPlayers;
        this.queuedInputCommands = queuedInputCommands;

        if (lastObservedMatchPhase is MatchPhase previous && previous != matchPhase)
        {
            logger.LogInformation(
                "Match phase changed from {PreviousPhase} to {CurrentPhase}.",
                FormatMatchPhase(previous),
                FormatMatchPhase(matchPhase));
        }

        this.matchPhase = matchPhase;
        lastObservedMatchPhase = matchPhase;
    }

    public void LobbyFilledWithBots(int addedBots, int totalBots, MatchStartReason reason)
    {
        logger.LogInformation(
            "Lobby filled with bots: added {AddedBots}, total bots {TotalBots}, reason {Reason}.",
            addedBots,
            totalBots,
            FormatMatchStartReason(reason));
    }

    public void BotInputDelaySampled(BotInputDelayDiagnostics diagnostics)
    {
        botInputLatencyMilliseconds = diagnostics.AverageOneWayLatencyMilliseconds;
        botInputDelayTicks = diagnostics.EffectiveDelayTicks;

        if (lastLoggedBotInputDelay == diagnostics)
            return;

        lastLoggedBotInputDelay = diagnostics;
        logger.LogInformation(
            "Bot input delay sampled: sampled_humans {SampledHumanCount}, average_one_way_latency_ms {AverageOneWayLatencyMilliseconds}, effective_delay_ticks {EffectiveDelayTicks}.",
            diagnostics.SampledHumanCount,
            diagnostics.AverageOneWayLatencyMilliseconds,
            diagnostics.EffectiveDelayTicks);
    }

    public void PeerConnected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
        logger.LogInformation(
            "Peer connected: peer_id {PeerId}, endpoint {Endpoint}.",
            peerId.Value,
            endpoint.ToString());
    }

    public void PeerDisconnected(
        NetworkPeerId peerId,
        NetworkDisconnectReason reason,
        InProcessClientConnection? connection)
    {
        if (connection is InProcessClientConnection accepted)
        {
            logger.LogInformation(
                "Peer disconnected: peer_id {PeerId}, connection_id {ConnectionId}, player_id {PlayerId}, reason {Reason}.",
                peerId.Value,
                accepted.ConnectionId.Value,
                accepted.PlayerId.Value,
                FormatDisconnectReason(reason));
        }
        else
        {
            logger.LogInformation(
                "Peer disconnected: peer_id {PeerId}, reason {Reason}.",
                peerId.Value,
                FormatDisconnectReason(reason));
        }
    }

    public void ClientAccepted(NetworkPeerId peerId, InProcessClientConnection connection)
    {
        ConnectionsAccepted.Add(1);
        logger.LogInformation(
            "Client accepted: peer_id {PeerId}, connection_id {ConnectionId}, player_id {PlayerId}.",
            peerId.Value,
            connection.ConnectionId.Value,
            connection.PlayerId.Value);
    }

    public void ClientDisconnected(NetworkDisconnectReason reason)
    {
        ConnectionsDisconnected.Add(1, new KeyValuePair<string, object?>("reason", FormatDisconnectReason(reason)));
    }

    public void PacketReceived(
        ProtocolMessageType? messageType,
        NetworkDelivery delivery,
        byte channel)
    {
        PacketsReceived.Add(
            1,
            new KeyValuePair<string, object?>("message_type", FormatMessageType(messageType)),
            new KeyValuePair<string, object?>("delivery", FormatDelivery(delivery)),
            new KeyValuePair<string, object?>("channel", channel));
    }

    public void HandshakeRejected(NetworkPeerId peerId, ServerRejectReason reason, string detail)
    {
        string formattedReason = FormatRejectReason(reason);
        HandshakesRejected.Add(1, new KeyValuePair<string, object?>("reason", formattedReason));
        PacketsInvalid.Add(1, new KeyValuePair<string, object?>("reason", $"handshake_{formattedReason}"));
        logger.LogWarning(
            "Handshake rejected: peer_id {PeerId}, reason {Reason}, detail {Detail}.",
            peerId.Value,
            formattedReason,
            detail);
    }

    public void InvalidInput(NetworkPeerId peerId, ServerInputRejectReason reason)
    {
        string formattedReason = FormatInputRejectReason(reason);
        PacketsInvalid.Add(1, new KeyValuePair<string, object?>("reason", formattedReason));
        logger.LogWarning(
            "Invalid input packet: peer_id {PeerId}, reason {Reason}.",
            peerId.Value,
            formattedReason);
    }

    public void InvalidCommand(NetworkPeerId peerId, InProcessClientConnection connection)
    {
        string reason = FormatInputRejectReason(ServerInputRejectReason.InvalidCommand);
        PacketsInvalid.Add(1, new KeyValuePair<string, object?>("reason", reason));
        logger.LogWarning(
            "Invalid input command: peer_id {PeerId}, connection_id {ConnectionId}, player_id {PlayerId}, reason {Reason}.",
            peerId.Value,
            connection.ConnectionId.Value,
            connection.PlayerId.Value,
            reason);
    }

    public void TickCompleted(TimeSpan duration)
    {
        TickDuration.Record(duration.TotalMilliseconds);
    }

    public void SnapshotsSent(int count)
    {
        if (count <= 0)
            return;

        SnapshotsSentCounter.Add(count);
        logger.LogInformation("Snapshot batch sent: count {SnapshotCount}.", count);
    }

    public void PlayerDebugStates(IReadOnlyList<ServerPlayerDebugState> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        if (players.Count == 0 ||
            players[0].ServerTick == 0 ||
            players[0].ServerTick % (ulong)playerDebugLogIntervalTicks != 0)
        {
            return;
        }

        foreach (ServerPlayerDebugState player in players)
        {
            logger.LogInformation(
                "Authoritative player debug state: server_tick {ServerTick}, peer_id {PeerId}, connection_id {ConnectionId}, player_id {PlayerId}, kind {PlayerKind}, position ({PositionX}, {PositionY}, {PositionZ}), velocity ({VelocityX}, {VelocityY}, {VelocityZ}), yaw {YawRadians}, pitch {PitchRadians}, stance {Stance}, sprinting {Sprinting}, capsule_height {CapsuleHeight}, health {CurrentHealth}/{MaxHealth}, alive {Alive}, weapon {WeaponId}, ammo {AmmoInMagazine}/{ReserveAmmo}, reloading {IsReloading}, last_input {LastProcessedInputSequence}, last_input_client_tick {LastProcessedInputClientTick}, queued_inputs {QueuedInputCount}.",
                player.ServerTick,
                player.PeerId,
                player.ConnectionId,
                player.PlayerId,
                player.Kind,
                player.Position.X,
                player.Position.Y,
                player.Position.Z,
                player.Velocity.X,
                player.Velocity.Y,
                player.Velocity.Z,
                player.YawRadians,
                player.PitchRadians,
                player.Stance,
                player.Sprinting,
                player.CapsuleHeight,
                player.CurrentHealth,
                player.MaxHealth,
                player.Alive,
                player.WeaponId,
                player.AmmoInMagazine,
                player.ReserveAmmo,
                player.IsReloading,
                player.LastProcessedInputSequence,
                player.LastProcessedInputClientTick,
                player.QueuedInputCount);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        lock (InstancesLock)
            Instances.Remove(this);

        disposed = true;
    }

    private static Measurement<int> ObserveConnectedPlayers()
    {
        lock (InstancesLock)
            return new Measurement<int>(Instances.Sum(instance => instance.connectedPlayers));
    }

    private static Measurement<int> ObserveActivePlayers()
    {
        lock (InstancesLock)
            return new Measurement<int>(Instances.Sum(instance => instance.activePlayers));
    }

    private static Measurement<int> ObserveBotPlayers()
    {
        lock (InstancesLock)
            return new Measurement<int>(Instances.Sum(instance => instance.botPlayers));
    }

    private static Measurement<int> ObserveLivingPlayers()
    {
        lock (InstancesLock)
            return new Measurement<int>(Instances.Sum(instance => instance.livingPlayers));
    }

    private static Measurement<int> ObserveInputQueueDepth()
    {
        lock (InstancesLock)
            return new Measurement<int>(Instances.Sum(instance => instance.queuedInputCommands));
    }

    private static Measurement<double> ObserveBotInputLatency()
    {
        lock (InstancesLock)
            return new Measurement<double>(Instances.Sum(instance => instance.botInputLatencyMilliseconds));
    }

    private static Measurement<int> ObserveBotInputDelayTicks()
    {
        lock (InstancesLock)
            return new Measurement<int>(Instances.Sum(instance => instance.botInputDelayTicks));
    }

    private static IEnumerable<Measurement<int>> ObserveMatchPhase()
    {
        Dictionary<MatchPhase, int> phaseCounts = [];
        lock (InstancesLock)
        {
            foreach (ServerObservability instance in Instances)
                phaseCounts[instance.matchPhase] = phaseCounts.GetValueOrDefault(instance.matchPhase) + 1;
        }

        foreach (MatchPhase phase in MatchPhases)
        {
            yield return new Measurement<int>(
                phaseCounts.GetValueOrDefault(phase),
                new KeyValuePair<string, object?>("phase", FormatMatchPhase(phase)));
        }
    }

    private static string FormatMessageType(ProtocolMessageType? messageType) =>
        messageType switch
        {
            ProtocolMessageType.ClientHello => "client_hello",
            ProtocolMessageType.ServerAccept => "server_accept",
            ProtocolMessageType.ServerReject => "server_reject",
            ProtocolMessageType.ClientInput => "client_input",
            ProtocolMessageType.ServerSnapshot => "server_snapshot",
            ProtocolMessageType.ServerEvent => "server_event",
            ProtocolMessageType.ClientDisconnect => "client_disconnect",
            ProtocolMessageType.ServerDisconnect => "server_disconnect",
            null => "unknown",
            _ => "unknown",
        };

    private static string FormatDelivery(NetworkDelivery delivery) =>
        delivery switch
        {
            NetworkDelivery.Unreliable => "unreliable",
            NetworkDelivery.ReliableUnordered => "reliable_unordered",
            NetworkDelivery.Sequenced => "sequenced",
            NetworkDelivery.ReliableOrdered => "reliable_ordered",
            NetworkDelivery.ReliableSequenced => "reliable_sequenced",
            _ => "unknown",
        };

    private static string FormatDisconnectReason(NetworkDisconnectReason reason) =>
        reason switch
        {
            NetworkDisconnectReason.ConnectionFailed => "connection_failed",
            NetworkDisconnectReason.Timeout => "timeout",
            NetworkDisconnectReason.HostUnreachable => "host_unreachable",
            NetworkDisconnectReason.NetworkUnreachable => "network_unreachable",
            NetworkDisconnectReason.RemoteConnectionClose => "remote_connection_close",
            NetworkDisconnectReason.LocalDisconnect => "local_disconnect",
            NetworkDisconnectReason.ConnectionRejected => "connection_rejected",
            NetworkDisconnectReason.InvalidProtocol => "invalid_protocol",
            NetworkDisconnectReason.UnknownHost => "unknown_host",
            NetworkDisconnectReason.Reconnect => "reconnect",
            NetworkDisconnectReason.PeerToPeerConnection => "peer_to_peer_connection",
            NetworkDisconnectReason.PeerNotFound => "peer_not_found",
            NetworkDisconnectReason.Unknown => "unknown",
            _ => "unknown",
        };

    private static string FormatRejectReason(ServerRejectReason reason) =>
        reason switch
        {
            ServerRejectReason.MalformedPacket => "malformed_packet",
            ServerRejectReason.UnsupportedProtocolVersion => "unsupported_protocol_version",
            ServerRejectReason.IncompatibleBuild => "incompatible_build",
            ServerRejectReason.IncompatibleContent => "incompatible_content",
            ServerRejectReason.UnexpectedMessageType => "unexpected_message_type",
            ServerRejectReason.AcceptFailed => "accept_failed",
            ServerRejectReason.MatchUnavailable => "match_unavailable",
            _ => "unknown",
        };

    private static string FormatInputRejectReason(ServerInputRejectReason reason) =>
        reason switch
        {
            ServerInputRejectReason.MalformedFrame => "malformed_frame",
            ServerInputRejectReason.UnexpectedMessageType => "unexpected_message_type",
            ServerInputRejectReason.WrongSession => "wrong_session",
            ServerInputRejectReason.MalformedPayload => "malformed_payload",
            ServerInputRejectReason.InvalidCommand => "invalid_command",
            _ => "unknown",
        };

    private static string FormatMatchPhase(MatchPhase phase) =>
        phase switch
        {
            MatchPhase.WaitingForPlayers => "waiting_for_players",
            MatchPhase.Countdown => "countdown",
            MatchPhase.Playing => "playing",
            MatchPhase.Finished => "finished",
            MatchPhase.Resetting => "resetting",
            _ => "unknown",
        };

    private static string FormatMatchStartReason(MatchStartReason reason) =>
        reason switch
        {
            MatchStartReason.HumanMinimumReached => "human_minimum_reached",
            MatchStartReason.WaitingExpired => "waiting_expired",
            MatchStartReason.ForceStart => "force_start",
            _ => "unknown",
        };
}
