using System.Net;
using LiteNetLib;

namespace Royale.Network;

public sealed class LiteNetLibNetworkTransport : INetworkTransport
{
    private readonly EventBasedNetListener _listener;
    private readonly NetManager _manager;
    private readonly Dictionary<NetworkPeerId, NetPeer> _peers = new();
    private INetworkEventHandler? _pollHandler;
    private bool _started;
    private bool _disposed;

    public LiteNetLibNetworkTransport()
    {
        _listener = new EventBasedNetListener();
        _listener.ConnectionRequestEvent += request => request.Accept();
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
        _listener.NetworkErrorEvent += OnNetworkError;
        _listener.NetworkLatencyUpdateEvent += OnNetworkLatencyUpdate;

        _manager = new NetManager(_listener)
        {
            AutoRecycle = true,
            ChannelsCount = 64,
        };
    }

    public void Start(int port)
    {
        ThrowIfDisposed();

        if (_started)
        {
            throw new InvalidOperationException("Network transport has already been started.");
        }

        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Listen port must be between 0 and 65535.");
        }

        if (!_manager.Start(port))
        {
            throw new InvalidOperationException($"Network transport failed to start on UDP port {port}.");
        }

        _started = true;
    }

    public NetworkPeerId Connect(NetworkEndpoint endpoint)
    {
        ThrowIfDisposed();
        EnsureStarted();

        NetPeer peer = _manager.Connect(endpoint.Host, endpoint.Port, string.Empty)
            ?? throw new InvalidOperationException($"Network transport could not start a connection to {endpoint}.");

        NetworkPeerId peerId = ToPeerId(peer);
        _peers[peerId] = peer;
        return peerId;
    }

    public void Send(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel = 0)
    {
        ThrowIfDisposed();
        EnsureStarted();

        if (channel > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Network delivery channel must be between 0 and 63.");
        }

        if (!_peers.TryGetValue(peerId, out NetPeer? peer) || peer.ConnectionState != ConnectionState.Connected)
        {
            throw new InvalidOperationException($"Network peer {peerId} is not connected.");
        }

        peer.Send(packet, channel, delivery.ToLiteNetLib());
    }

    public void Disconnect(NetworkPeerId peerId)
    {
        ThrowIfDisposed();
        EnsureStarted();

        if (!_peers.TryGetValue(peerId, out NetPeer? peer))
        {
            throw new InvalidOperationException($"Network peer {peerId} is unknown.");
        }

        _manager.DisconnectPeer(peer);
    }

    public void Poll(INetworkEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();
        EnsureStarted();

        _pollHandler = handler;
        try
        {
            _manager.PollEvents();
        }
        finally
        {
            _pollHandler = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _manager.Stop();
        _peers.Clear();
        _pollHandler = null;
        _disposed = true;
    }

    private void OnPeerConnected(NetPeer peer)
    {
        NetworkPeerId peerId = ToPeerId(peer);
        _peers[peerId] = peer;
        _pollHandler?.Connected(peerId, ToEndpoint(peer));
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        NetworkPeerId peerId = ToPeerId(peer);
        _peers.Remove(peerId);
        _pollHandler?.Disconnected(peerId, disconnectInfo.Reason.ToNetworkDisconnectReason());
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
    {
        byte[] packet = new byte[reader.UserDataSize];
        Buffer.BlockCopy(reader.RawData, reader.UserDataOffset, packet, 0, packet.Length);
        _pollHandler?.PacketReceived(ToPeerId(peer), packet, delivery.ToNetworkDelivery(), channel);
    }

    private void OnNetworkError(IPEndPoint endpoint, System.Net.Sockets.SocketError socketError)
    {
        NetworkEndpoint? networkEndpoint = endpoint is null
            ? null
            : new NetworkEndpoint(endpoint.Address.ToString(), endpoint.Port);

        _pollHandler?.NetworkError(networkEndpoint, socketError);
    }

    private void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        _pollHandler?.LatencyUpdated(ToPeerId(peer), latency);
    }

    private static NetworkPeerId ToPeerId(NetPeer peer) => new(peer.Id);

    private static NetworkEndpoint ToEndpoint(NetPeer peer) => new(peer.Address.ToString(), peer.Port);

    private void EnsureStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Network transport has not been started.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LiteNetLibNetworkTransport));
        }
    }
}
