using System.Net.Sockets;

namespace Royale.Network;

public sealed class SimulatedNetworkTransport : INetworkTransport, INetworkTransportDiagnostics
{
    private readonly INetworkTransport _inner;
    private readonly TimeProvider _timeProvider;
    private SimulatedNetworkConditions _conditions;
    private Random _random;
    private readonly List<PendingPacket> _pendingSends = [];
    private readonly List<PendingPacket> _pendingReceives = [];
    private long _nextPacketOrder;
    private bool _disposed;

    public SimulatedNetworkTransport(
        INetworkTransport inner,
        SimulatedNetworkConditions? conditions = null,
        TimeProvider? timeProvider = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _conditions = conditions ?? SimulatedNetworkConditions.None;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _random = _conditions.RandomSeed is int seed ? new Random(seed) : new Random();
    }

    public SimulatedNetworkConditions CurrentConditions
    {
        get
        {
            ThrowIfDisposed();
            return _conditions;
        }
    }

    public void SetConditions(SimulatedNetworkConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ThrowIfDisposed();

        _conditions = conditions;
        _random = conditions.RandomSeed is int seed ? new Random(seed) : new Random();
    }

    public void Start(int port)
    {
        ThrowIfDisposed();
        _inner.Start(port);
    }

    public NetworkPeerId Connect(NetworkEndpoint endpoint)
    {
        ThrowIfDisposed();
        return _inner.Connect(endpoint);
    }

    public void Send(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel = 0)
    {
        ThrowIfDisposed();
        FlushDueSends();

        if (!_conditions.ImpairsPackets)
        {
            _inner.Send(peerId, packet, delivery, channel);
            return;
        }

        QueueSend(peerId, packet, delivery, channel);
        FlushDueSends();
    }

    public void Disconnect(NetworkPeerId peerId)
    {
        ThrowIfDisposed();
        _inner.Disconnect(peerId);
        RemovePendingPackets(peerId);
    }

    public void Poll(INetworkEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();

        FlushDueSends();
        _inner.Poll(new InterceptingEventHandler(this, handler));
        FlushDueSends();
        FlushDueReceives(handler);
    }

    public bool TryGetPeerStatistics(NetworkPeerId peerId, out NetworkPeerStatistics statistics)
    {
        ThrowIfDisposed();

        if (_inner is INetworkTransportDiagnostics diagnostics)
            return diagnostics.TryGetPeerStatistics(peerId, out statistics);

        statistics = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pendingSends.Clear();
        _pendingReceives.Clear();
        _inner.Dispose();
        _disposed = true;
    }

    private void QueueSend(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel)
    {
        if (ShouldDrop())
        {
            return;
        }

        Enqueue(_pendingSends, peerId, packet, delivery, channel);

        if (ShouldDuplicate())
        {
            Enqueue(_pendingSends, peerId, packet, delivery, channel);
        }
    }

    private void QueueReceive(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
    {
        if (!_conditions.ImpairsPackets)
        {
            return;
        }

        if (ShouldDrop())
        {
            return;
        }

        Enqueue(_pendingReceives, peerId, packet.Span, delivery, channel);

        if (ShouldDuplicate())
        {
            Enqueue(_pendingReceives, peerId, packet.Span, delivery, channel);
        }
    }

    private void Enqueue(
        List<PendingPacket> queue,
        NetworkPeerId peerId,
        ReadOnlySpan<byte> packet,
        NetworkDelivery delivery,
        byte channel)
    {
        byte[] payload = packet.ToArray();
        queue.Add(new PendingPacket(
            GetDueTime(),
            _nextPacketOrder++,
            GetReorderPriority(),
            peerId,
            payload,
            delivery,
            channel));
    }

    private int? GetReorderPriority() =>
        ChanceOccurs(_conditions.ReorderChance) ? _random.Next() : null;

    private DateTimeOffset GetDueTime()
    {
        TimeSpan delay = _conditions.Latency;

        if (_conditions.Jitter > TimeSpan.Zero)
        {
            double jitterTicks = ((_random.NextDouble() * 2) - 1) * _conditions.Jitter.Ticks;
            delay += TimeSpan.FromTicks((long)Math.Round(jitterTicks));
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }
        }

        return _timeProvider.GetUtcNow() + delay;
    }

    private void FlushDueSends()
    {
        List<PendingPacket> duePackets = TakeDuePackets(_pendingSends);
        ReorderIfNeeded(duePackets);

        foreach (PendingPacket packet in duePackets)
        {
            _inner.Send(packet.PeerId, packet.Payload, packet.Delivery, packet.Channel);
        }
    }

    private void FlushDueReceives(INetworkEventHandler handler)
    {
        List<PendingPacket> duePackets = TakeDuePackets(_pendingReceives);
        ReorderIfNeeded(duePackets);

        foreach (PendingPacket packet in duePackets)
        {
            handler.PacketReceived(packet.PeerId, packet.Payload, packet.Delivery, packet.Channel);
        }
    }

    private List<PendingPacket> TakeDuePackets(List<PendingPacket> queue)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<PendingPacket> duePackets = [];

        for (int i = 0; i < queue.Count;)
        {
            if (queue[i].DueTime <= now)
            {
                duePackets.Add(queue[i]);
                queue.RemoveAt(i);
                continue;
            }

            i++;
        }

        return duePackets;
    }

    private void RemovePendingPackets(NetworkPeerId peerId)
    {
        _pendingSends.RemoveAll(packet => packet.PeerId == peerId);
        _pendingReceives.RemoveAll(packet => packet.PeerId == peerId);
    }

    private void ReorderIfNeeded(List<PendingPacket> duePackets)
    {
        if (duePackets.Count <= 1 || !duePackets.Any(packet => packet.ReorderPriority.HasValue))
        {
            return;
        }

        long[] originalOrder = duePackets.Select(packet => packet.Order).ToArray();
        duePackets.Sort(static (left, right) =>
        {
            if (left.ReorderPriority is int leftPriority && right.ReorderPriority is int rightPriority)
                return leftPriority.CompareTo(rightPriority);

            if (left.ReorderPriority.HasValue)
                return -1;

            if (right.ReorderPriority.HasValue)
                return 1;

            return left.Order.CompareTo(right.Order);
        });

        bool unchanged = true;
        for (int i = 0; i < duePackets.Count; i++)
        {
            if (duePackets[i].Order != originalOrder[i])
            {
                unchanged = false;
                break;
            }
        }

        if (unchanged)
        {
            PendingPacket first = duePackets[0];
            duePackets.RemoveAt(0);
            duePackets.Add(first);
        }
    }

    private bool ShouldDrop() => ChanceOccurs(_conditions.LossChance);

    private bool ShouldDuplicate() => ChanceOccurs(_conditions.DuplicateChance);

    private bool ChanceOccurs(double chance) => chance > 0 && _random.NextDouble() < chance;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SimulatedNetworkTransport));
        }
    }

    private sealed record PendingPacket(
        DateTimeOffset DueTime,
        long Order,
        int? ReorderPriority,
        NetworkPeerId PeerId,
        byte[] Payload,
        NetworkDelivery Delivery,
        byte Channel);

    private sealed class InterceptingEventHandler : INetworkEventHandler
    {
        private readonly SimulatedNetworkTransport _transport;
        private readonly INetworkEventHandler _outerHandler;

        public InterceptingEventHandler(SimulatedNetworkTransport transport, INetworkEventHandler outerHandler)
        {
            _transport = transport;
            _outerHandler = outerHandler;
        }

        public void Connected(NetworkPeerId peerId, NetworkEndpoint endpoint)
        {
            _outerHandler.Connected(peerId, endpoint);
        }

        public void Disconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
        {
            _transport.RemovePendingPackets(peerId);
            _outerHandler.Disconnected(peerId, reason);
        }

        public void PacketReceived(NetworkPeerId peerId, ReadOnlyMemory<byte> packet, NetworkDelivery delivery, byte channel)
        {
            if (!_transport._conditions.ImpairsPackets)
            {
                _outerHandler.PacketReceived(peerId, packet, delivery, channel);
                return;
            }

            _transport.QueueReceive(peerId, packet, delivery, channel);
        }

        public void NetworkError(NetworkEndpoint? endpoint, SocketError socketError)
        {
            _outerHandler.NetworkError(endpoint, socketError);
        }

        public void LatencyUpdated(NetworkPeerId peerId, int latencyMilliseconds)
        {
            _outerHandler.LatencyUpdated(peerId, latencyMilliseconds);
        }
    }
}
