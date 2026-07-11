using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
using Royale.Simulation.World;
using WattleScript.Interpreter;

namespace Royale.Gameplay.Tests.Scenarios;

internal interface IScenarioRuntime : IDisposable
{
    bool IsDisposed { get; }

    ulong CurrentTick { get; }

    int ConnectedPlayerCount { get; }

    int ParticipantCount { get; }

    int BotPlayerCount { get; }

    int LivingPlayerCount { get; }

    ScenarioPlayerHandle ConnectPlayer();

    void DisconnectPlayer(ScenarioPlayerHandle player);

    bool TrySendInput(ScenarioPlayerHandle player, PlayerInputCommand command);

    ServerSnapshot GetLatestSnapshot(ScenarioPlayerHandle player);

    IReadOnlyList<ServerPlayerDebugState> GetPlayerDebugStates();

    ForceStartResult ForceStart();

    void Step();
}

internal abstract class ScenarioPlayerHandle
{
    public abstract uint PlayerId { get; }

    public abstract uint ConnectionId { get; }

    public bool IsConnected { get; private set; } = true;

    public ServerSnapshot? LatestSnapshot { get; private set; }

    public void UpdateLatestSnapshot(ServerSnapshot snapshot)
    {
        LatestSnapshot = snapshot;
    }

    public void MarkDisconnected()
    {
        IsConnected = false;
    }
}

internal sealed class InProcessScenarioRuntime : IScenarioRuntime
{
    private readonly InProcessServerSession session;

    private InProcessScenarioRuntime(InProcessServerSession session)
    {
        this.session = session;
    }

    public bool IsDisposed => session.IsDisposed;

    public ulong CurrentTick => session.CurrentTick;

    public int ConnectedPlayerCount => session.ConnectedClientCount;

    public int ParticipantCount => session.ActivePlayerCount;

    public int BotPlayerCount => session.BotPlayerCount;

    public int LivingPlayerCount => session.LivingPlayerCount;

    public static InProcessScenarioRuntime Start(string mapId) => new(InProcessServerSession.Create(mapId));

    public ScenarioPlayerHandle ConnectPlayer()
    {
        InProcessClientConnection connection = session.ConnectClient();
        return new InProcessScenarioPlayerHandle(connection);
    }

    public void DisconnectPlayer(ScenarioPlayerHandle player)
    {
        InProcessScenarioPlayerHandle inProcessPlayer = RequireInProcessPlayer(player);
        session.DisconnectClient(inProcessPlayer.Connection);
        inProcessPlayer.MarkDisconnected();
    }

    public bool TrySendInput(ScenarioPlayerHandle player, PlayerInputCommand command)
    {
        InProcessScenarioPlayerHandle inProcessPlayer = RequireInProcessPlayer(player);
        return session.TryEnqueueInputCommand(inProcessPlayer.Connection, command);
    }

    public ServerSnapshot GetLatestSnapshot(ScenarioPlayerHandle player)
    {
        InProcessScenarioPlayerHandle inProcessPlayer = RequireInProcessPlayer(player);
        IReadOnlyList<ServerSnapshot> snapshots = session.DrainSnapshots(inProcessPlayer.Connection);

        if (snapshots.Count > 0)
            inProcessPlayer.UpdateLatestSnapshot(snapshots[^1]);

        return inProcessPlayer.LatestSnapshot
            ?? throw new ScriptRuntimeException(
                $"No snapshot has been produced for scenario player '{inProcessPlayer.PlayerId}'.");
    }

    public IReadOnlyList<ServerPlayerDebugState> GetPlayerDebugStates() => session.GetPlayerDebugStates();

    public ForceStartResult ForceStart() => session.ForceStart();

    public void Step()
    {
        session.Step();
    }

    public void Dispose()
    {
        session.Dispose();
    }

    private static InProcessScenarioPlayerHandle RequireInProcessPlayer(ScenarioPlayerHandle player) =>
        player as InProcessScenarioPlayerHandle
            ?? throw new ScriptRuntimeException("scenario player belongs to a different runtime.");

    private sealed class InProcessScenarioPlayerHandle(InProcessClientConnection connection) : ScenarioPlayerHandle
    {
        public InProcessClientConnection Connection { get; } = connection;

        public override uint PlayerId => Connection.PlayerId.Value;

        public override uint ConnectionId => Connection.ConnectionId.Value;
    }
}

internal sealed class UdpScenarioRuntime : IScenarioRuntime
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FixedSimulationStep =
        TimeSpan.FromSeconds(1.0 / SimulationSettings.TickRateHz);

    private readonly LiteNetLibNetworkTransport serverTransport;
    private readonly NetworkServerRuntime server;
    private readonly ScenarioTimeProvider timeProvider;
    private readonly List<UdpScenarioClient> clients = [];
    private readonly int serverPort;
    private bool disposed;

    private UdpScenarioRuntime(
        LiteNetLibNetworkTransport serverTransport,
        NetworkServerRuntime server,
        ScenarioTimeProvider timeProvider,
        int serverPort)
    {
        this.serverTransport = serverTransport;
        this.server = server;
        this.timeProvider = timeProvider;
        this.serverPort = serverPort;
    }

    public bool IsDisposed => disposed;

    public ulong CurrentTick => server.CurrentTick;

    public int ConnectedPlayerCount => server.ConnectedClientCount;

    public int ParticipantCount => server.ActivePlayerCount;

    public int BotPlayerCount => server.BotPlayerCount;

    public int LivingPlayerCount => server.LivingPlayerCount;

    public static UdpScenarioRuntime Start(string mapId)
    {
        int port = ReserveUdpPort();
        var transport = new LiteNetLibNetworkTransport();
        transport.Start(port);

        try
        {
            var timeProvider = new ScenarioTimeProvider();
            var runtime = new NetworkServerRuntime(
                transport,
                InProcessServerSession.Create(mapId));
            return new UdpScenarioRuntime(transport, runtime, timeProvider, port);
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    public ScenarioPlayerHandle ConnectPlayer()
    {
        ThrowIfDisposed();

        var transport = new SimulatedNetworkTransport(
            new LiteNetLibNetworkTransport(),
            SimulatedNetworkConditions.None,
            timeProvider);
        transport.Start(port: 0);

        UdpScenarioClient client;
        try
        {
            client = new UdpScenarioClient(transport, new NetworkEndpoint("127.0.0.1", serverPort));
        }
        catch
        {
            transport.Dispose();
            throw;
        }

        clients.Add(client);

        try
        {
            PumpUntil(
                () => client.IsAccepted && client.LatestSnapshot is not null,
                HandshakeTimeout,
                $"UDP scenario client did not complete handshake and receive a first snapshot within {HandshakeTimeout.TotalSeconds:0} seconds.");
        }
        catch
        {
            clients.Remove(client);
            client.Dispose();
            throw;
        }

        return new UdpScenarioPlayerHandle(client);
    }

    public void DisconnectPlayer(ScenarioPlayerHandle player)
    {
        UdpScenarioPlayerHandle udpPlayer = RequireUdpPlayer(player);
        UdpScenarioClient client = udpPlayer.Client;
        int expectedConnectedCount = Math.Max(0, ConnectedPlayerCount - 1);

        client.Disconnect();
        PumpUntil(
            () => ConnectedPlayerCount <= expectedConnectedCount,
            DisconnectTimeout,
            $"UDP scenario client did not disconnect within {DisconnectTimeout.TotalSeconds:0} seconds.");

        client.Dispose();
        clients.Remove(client);
        udpPlayer.MarkDisconnected();
    }

    public bool TrySendInput(ScenarioPlayerHandle player, PlayerInputCommand command)
    {
        UdpScenarioPlayerHandle udpPlayer = RequireUdpPlayer(player);
        return udpPlayer.Client.TrySendInput(command);
    }

    public ServerSnapshot GetLatestSnapshot(ScenarioPlayerHandle player)
    {
        UdpScenarioPlayerHandle udpPlayer = RequireUdpPlayer(player);
        PollClients();

        return udpPlayer.Client.LatestSnapshot
            ?? throw new ScriptRuntimeException(
                $"No snapshot has been produced for scenario player '{udpPlayer.PlayerId}'.");
    }

    public IReadOnlyList<ServerPlayerDebugState> GetPlayerDebugStates()
    {
        ThrowIfDisposed();
        return server.GetPlayerDebugStates();
    }

    public ForceStartResult ForceStart()
    {
        ThrowIfDisposed();
        return server.ForceStart();
    }

    public SimulatedNetworkConditions GetNetworkConditions(ScenarioPlayerHandle player)
    {
        UdpScenarioPlayerHandle udpPlayer = RequireUdpPlayer(player);
        return udpPlayer.Client.NetworkTransport.CurrentConditions;
    }

    public void SetNetworkConditions(ScenarioPlayerHandle player, SimulatedNetworkConditions conditions)
    {
        UdpScenarioPlayerHandle udpPlayer = RequireUdpPlayer(player);
        udpPlayer.Client.NetworkTransport.SetConditions(conditions);
    }

    public void Step()
    {
        ThrowIfDisposed();
        timeProvider.Advance(FixedSimulationStep);
        PollClients();
        if (clients.Count > 0)
            Thread.Sleep(1);
        server.Step();
        PollClients();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        foreach (UdpScenarioClient client in clients)
            client.Dispose();

        clients.Clear();
        server.Dispose();
        serverTransport.Dispose();
        disposed = true;
    }

    private void PumpUntil(Func<bool> condition, TimeSpan timeout, string? timeoutMessage)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed >= timeout)
            {
                if (timeoutMessage is null)
                    return;

                throw new ScriptRuntimeException(timeoutMessage);
            }

            Step();
            Thread.Sleep(1);
        }
    }

    private void PollClients()
    {
        foreach (UdpScenarioClient client in clients.ToArray())
            client.Poll();
    }

    private static UdpScenarioPlayerHandle RequireUdpPlayer(ScenarioPlayerHandle player) =>
        player as UdpScenarioPlayerHandle
            ?? throw new ScriptRuntimeException("scenario player belongs to a different runtime.");

    private static int ReserveUdpPort()
    {
        using UdpClient udpClient = new(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed class UdpScenarioPlayerHandle(UdpScenarioClient client) : ScenarioPlayerHandle
    {
        public UdpScenarioClient Client { get; } = client;

        public override uint PlayerId => Client.PlayerId;

        public override uint ConnectionId => Client.ConnectionId;
    }

    private sealed class UdpScenarioClient : INetworkEventHandler, IDisposable
    {
        private readonly SimulatedNetworkTransport transport;
        private readonly NetworkPeerId serverPeerId;
        private NetworkHandshakeClient? handshake;
        private ClientInputSender? inputSender;
        private bool disposed;

        public UdpScenarioClient(SimulatedNetworkTransport transport, NetworkEndpoint serverEndpoint)
        {
            this.transport = transport;
            serverPeerId = transport.Connect(serverEndpoint);
        }

        public bool IsAccepted => inputSender is not null;

        public SimulatedNetworkTransport NetworkTransport => transport;

        public uint PlayerId => handshake?.AcceptedSession?.PlayerId ?? 0;

        public uint ConnectionId => handshake?.AcceptedSession?.ConnectionId ?? 0;

        public ServerSnapshot? LatestSnapshot { get; private set; }

        public void Poll()
        {
            if (!disposed)
                transport.Poll(this);
        }

        public bool TrySendInput(PlayerInputCommand command)
        {
            if (inputSender is null)
                throw new ScriptRuntimeException("scenario UDP player handshake has not completed.");

            return inputSender.TrySend(command);
        }

        public void Disconnect()
        {
            if (!disposed)
                transport.Disconnect(serverPeerId);
        }

        public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
        {
            if (peerId == serverPeerId && handshake is null)
                handshake = new NetworkHandshakeClient(transport, serverPeerId);
        }

        public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
        {
            handshake?.Disconnected(peerId, reason);
            if (peerId == serverPeerId)
                inputSender = null;
        }

        public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
        {
            if (peerId != serverPeerId)
                return;

            handshake?.PacketReceived(peerId, packet, delivery, channel);
            EnsureInputSender();
            TryApplySnapshot(packet, channel);
        }

        public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
        {
        }

        public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
        {
        }

        public void Dispose()
        {
            if (disposed)
                return;

            transport.Dispose();
            disposed = true;
        }

        private void EnsureInputSender()
        {
            if (inputSender is not null ||
                handshake?.State != NetworkHandshakeClientState.Accepted ||
                handshake.AcceptedSession is not ServerAccept acceptedSession)
            {
                return;
            }

            inputSender = new ClientInputSender(transport, serverPeerId, acceptedSession);
        }

        private void TryApplySnapshot(ReadOnlyMemory<byte> packet, byte channel)
        {
            if (inputSender is null ||
                channel != ServerSnapshotSender.SnapshotChannel ||
                handshake?.AcceptedSession is not ServerAccept acceptedSession)
            {
                return;
            }

            if (!ProtocolPacketFramer.TryReadPacket(
                packet.Span,
                out ProtocolPacketHeader header,
                out ReadOnlySpan<byte> payload,
                out _) ||
                header.MessageType != ProtocolMessageType.ServerSnapshot ||
                header.SessionId != acceptedSession.SessionId ||
                !ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out ServerSnapshot? snapshot) ||
                snapshot is null)
            {
                return;
            }

            LatestSnapshot = snapshot;
        }
    }

    private sealed class ScenarioTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }
}
