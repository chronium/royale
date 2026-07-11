using System.Net.Sockets;
using System.Numerics;
using Royale.Client.Gameplay;
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
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Networking;

public sealed class NetworkClientRuntime : INetworkEventHandler, IDisposable
{
    private readonly INetworkTransport transport;
    private readonly NetworkEndpoint serverEndpoint;
    private readonly NetworkPeerId serverPeerId;
    private readonly PlayerLookSettings lookSettings;
    private readonly ClientMovementPrediction prediction;
    private readonly RemoteSnapshotInterpolator remoteSnapshotInterpolator = new();
    private NetworkHandshakeClient? handshake;
    private ClientInputSender? inputSender;
    private uint nextCommandSequence = 1;
    private bool lookSeededFromSnapshot;
    private bool disposed;

    public NetworkClientRuntime(
        INetworkTransport transport,
        NetworkEndpoint serverEndpoint,
        PlayerLookSettings? lookSettings = null,
        Func<string, GameMap>? loadPredictionMap = null)
    {
        this.transport = transport;
        this.serverEndpoint = serverEndpoint;
        this.lookSettings = lookSettings ?? PlayerLookSettings.Default;
        prediction = new ClientMovementPrediction(loadPredictionMap ?? MapCatalog.LoadById);
        serverPeerId = transport.Connect(serverEndpoint);
    }

    public ClientNetworkState State { get; } = new();

    public ClientNetworkDiagnostics Diagnostics { get; } = new();

    public NetworkPeerId ServerPeerId => serverPeerId;

    public NetworkEndpoint ServerEndpoint => serverEndpoint;

    public PlayerLookState LookState { get; private set; }

    public bool HandshakeStarted => handshake is not null;

    public bool Accepted => inputSender is not null;

    public NetworkHandshakeClientState? HandshakeState => handshake?.State;

    public ServerAccept? AcceptedSession => handshake?.AcceptedSession;

    public ServerReject? HandshakeRejection => handshake?.Rejection;

    public bool PredictionMapAvailable => prediction.MapAvailable;

    internal MapStaticCollisionWorld? PredictionCollisionWorld => prediction.CollisionWorld;

    public bool PredictionSeeded => prediction.Seeded;

    public bool PredictionActive => prediction.Active;

    public bool? PredictionIsGrounded => prediction.IsGrounded;

    public int? PredictionStaticColliderCount => prediction.StaticColliderCount;

    public int PendingInputCount => prediction.PendingInputCount;

    public float LastPredictionCorrectionDistance => prediction.LastCorrectionDistance;

    public int LastReplayedInputCount => prediction.LastReplayedInputCount;

    public ulong ReconciliationCount => prediction.ReconciliationCount;

    public RemoteSnapshotInterpolator RemoteSnapshotInterpolator => remoteSnapshotInterpolator;

    public int RemoteSnapshotBufferCount => remoteSnapshotInterpolator.BufferedSnapshotCount;

    public ulong RemoteInterpolationDelayTicks => remoteSnapshotInterpolator.InterpolationDelayTicks;

    public double LastRemoteInterpolationTargetTick => remoteSnapshotInterpolator.LastInterpolationTargetTick;

    public bool LastRemoteRenderUsedInterpolation => remoteSnapshotInterpolator.LastRenderUsedInterpolation;

    public NetworkPeerStatistics? LastTransportStatistics { get; private set; }

    public static NetworkClientRuntime Connect(string host, int port)
    {
        var transport = new LiteNetLibNetworkTransport();
        transport.Start(port: 0);

        try
        {
            return new NetworkClientRuntime(transport, new NetworkEndpoint(host, port));
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    public void Poll()
    {
        ThrowIfDisposed();
        CacheTransportStatistics();
        transport.Poll(this);
        CacheTransportStatistics();
    }

    public void ApplyLook(PlayerInputSample input)
    {
        ThrowIfDisposed();
        LookState = PlayerLookController.ApplyMouseDelta(LookState, input.LookDelta, lookSettings);
    }

    public bool FixedUpdate(PlayerInputSample input, ulong clientTick)
    {
        ThrowIfDisposed();

        if (inputSender is null || !lookSeededFromSnapshot)
            return false;

        PlayerInputCommand command = new(
            nextCommandSequence++,
            checked((uint)Math.Min(clientTick, uint.MaxValue)),
            NormalizeMove(input.Move),
            LookState.YawRadians,
            LookState.PitchRadians,
            ToButtons(input));

        if (!inputSender.TrySend(command))
            return false;

        Diagnostics.RecordSuccessfulInputSend();
        prediction.StoreSentInput(command);
        prediction.Step(command);
        return true;
    }

    public bool TryGetPredictedLocalPlayer(out PlayerSnapshotState player)
    {
        ThrowIfDisposed();
        return prediction.TryGetPredictedLocalPlayer(out player);
    }

    public void AdvanceRemoteInterpolation(double deltaSeconds)
    {
        ThrowIfDisposed();
        remoteSnapshotInterpolator.Advance(deltaSeconds);
    }

    public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
    {
        if (peerId == serverPeerId && handshake is null)
        {
            handshake = new NetworkHandshakeClient(transport, serverPeerId);
            CacheTransportStatistics();
        }
    }

    public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
    {
        handshake?.Disconnected(peerId, reason);
        if (peerId == serverPeerId)
        {
            Diagnostics.RecordDisconnect(reason);
            inputSender = null;
            LookState = default;
            lookSeededFromSnapshot = false;
            prediction.Reset();
            remoteSnapshotInterpolator.Reset();
        }
    }

    public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        if (peerId != serverPeerId)
            return;

        Diagnostics.RecordPacketReceived();
        handshake?.PacketReceived(peerId, packet, delivery, channel);
        EnsureInputSender();
        TryApplySnapshot(packet, channel);
    }

    public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
    {
        Diagnostics.RecordNetworkError(endpoint, socketError);
    }

    public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
    {
        if (peerId == serverPeerId)
            Diagnostics.RecordLatency(latencyMilliseconds);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        prediction.Dispose();
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
        prediction.EnsureMapLoaded(acceptedSession.MapId);
    }

    private void TryApplySnapshot(ReadOnlyMemory<byte> packet, byte channel)
    {
        if (channel != ServerSnapshotSender.SnapshotChannel)
        {
            return;
        }

        ServerSnapshot? snapshot = null;
        bool valid = false;

        if (inputSender is not null &&
            handshake?.AcceptedSession is ServerAccept acceptedSession &&
            ProtocolPacketFramer.TryReadPacket(
            packet.Span,
            out ProtocolPacketHeader header,
            out ReadOnlySpan<byte> payload,
            out _) &&
            header.MessageType == ProtocolMessageType.ServerSnapshot &&
            header.SessionId == acceptedSession.SessionId &&
            ServerSnapshotPayloadSerializer.TryReadSnapshot(payload, out snapshot) &&
            snapshot is not null)
        {
            valid = true;
        }

        Diagnostics.RecordSnapshotPacket(valid);

        if (!valid)
        {
            return;
        }

        ServerSnapshot validSnapshot = snapshot!;
        State.ApplySnapshot(validSnapshot);
        SeedLookFromInitialSnapshot();
        remoteSnapshotInterpolator.AddSnapshot(validSnapshot);
        prediction.ApplySnapshot(validSnapshot);
    }

    private void SeedLookFromInitialSnapshot()
    {
        if (lookSeededFromSnapshot || !State.TryGetLocalPlayer(out PlayerSnapshotState localPlayer))
            return;

        LookState = new PlayerLookState(localPlayer.YawRadians, localPlayer.PitchRadians);
        lookSeededFromSnapshot = true;
    }

    private void CacheTransportStatistics()
    {
        if (transport is INetworkTransportDiagnostics diagnostics &&
            diagnostics.TryGetPeerStatistics(serverPeerId, out NetworkPeerStatistics statistics))
        {
            LastTransportStatistics = statistics;
        }
    }

    private static Vector2 NormalizeMove(Vector2 move)
    {
        if (!float.IsFinite(move.X) || !float.IsFinite(move.Y))
            return Vector2.Zero;

        return move.LengthSquared() > 1.0f
            ? Vector2.Normalize(move)
            : move;
    }

    private static InputButtons ToButtons(PlayerInputSample input)
    {
        InputButtons buttons = InputButtons.None;

        if (input.Jump)
            buttons |= InputButtons.Jump;

        if (input.Fire)
            buttons |= InputButtons.Fire;

        if (input.Crouch)
            buttons |= InputButtons.Crouch;

        if (input.Sprint)
            buttons |= InputButtons.Sprint;

        return buttons;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
