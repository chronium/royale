using System.Net.Sockets;
using System.Numerics;
using Royale.Client.Gameplay;
using Royale.Network;
using Royale.Protocol;
using Royale.Simulation.Movement;

namespace Royale.Client.Networking;

public sealed class NetworkClientRuntime : INetworkEventHandler, IDisposable
{
    private readonly INetworkTransport transport;
    private readonly NetworkPeerId serverPeerId;
    private readonly PlayerLookSettings lookSettings;
    private NetworkHandshakeClient? handshake;
    private ClientInputSender? inputSender;
    private uint nextCommandSequence = 1;
    private bool disposed;

    public NetworkClientRuntime(
        INetworkTransport transport,
        NetworkEndpoint serverEndpoint,
        PlayerLookSettings? lookSettings = null)
    {
        this.transport = transport;
        this.lookSettings = lookSettings ?? PlayerLookSettings.Default;
        serverPeerId = transport.Connect(serverEndpoint);
    }

    public ClientNetworkState State { get; } = new();

    public NetworkPeerId ServerPeerId => serverPeerId;

    public PlayerLookState LookState { get; private set; }

    public bool HandshakeStarted => handshake is not null;

    public bool Accepted => inputSender is not null;

    public NetworkHandshakeClientState? HandshakeState => handshake?.State;

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
        transport.Poll(this);
    }

    public void ApplyLook(PlayerInputSample input)
    {
        ThrowIfDisposed();
        LookState = PlayerLookController.ApplyMouseDelta(LookState, input.LookDelta, lookSettings);
    }

    public bool FixedUpdate(PlayerInputSample input, ulong clientTick)
    {
        ThrowIfDisposed();

        if (inputSender is null)
            return false;

        PlayerInputCommand command = new(
            nextCommandSequence++,
            checked((uint)Math.Min(clientTick, uint.MaxValue)),
            NormalizeMove(input.Move),
            LookState.YawRadians,
            LookState.PitchRadians,
            ToButtons(input));

        return inputSender.TrySend(command);
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

        State.ApplySnapshot(snapshot);
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

        return buttons;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
