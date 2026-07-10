using System.Net.Sockets;
using Royale.Network;

namespace Royale.Network.Tests;

public sealed class SimulatedNetworkTransportTests
{
    [Fact]
    public void NoneConditionsPassPacketsThrough()
    {
        FakeNetworkTransport inner = new();
        using SimulatedNetworkTransport transport = new(inner, SimulatedNetworkConditions.None);
        NetworkPeerId peerId = new(7);

        transport.Send(peerId, [1, 2, 3], NetworkDelivery.ReliableOrdered, channel: 4);

        SentPacket sent = Assert.Single(inner.SentPackets);
        Assert.Equal(peerId, sent.PeerId);
        Assert.Equal([1, 2, 3], sent.Payload);
        Assert.Equal(NetworkDelivery.ReliableOrdered, sent.Delivery);
        Assert.Equal(4, sent.Channel);

        RecordingNetworkEventHandler handler = new();
        inner.EnqueuePacket(peerId, [4, 5, 6], NetworkDelivery.Sequenced, channel: 2);

        transport.Poll(handler);

        ReceivedPacket received = Assert.Single(handler.Packets);
        Assert.Equal(peerId, received.PeerId);
        Assert.Equal([4, 5, 6], received.Payload);
        Assert.Equal(NetworkDelivery.Sequenced, received.Delivery);
        Assert.Equal(2, received.Channel);
    }

    [Fact]
    public void LatencyDelaysOutboundSendsAndInboundReceives()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(latency: TimeSpan.FromMilliseconds(50)),
            timeProvider);
        NetworkPeerId peerId = new(1);

        transport.Send(peerId, [9], NetworkDelivery.ReliableOrdered, channel: 3);
        Assert.Empty(inner.SentPackets);

        transport.Poll(new RecordingNetworkEventHandler());
        Assert.Empty(inner.SentPackets);

        timeProvider.Advance(TimeSpan.FromMilliseconds(49));
        transport.Poll(new RecordingNetworkEventHandler());
        Assert.Empty(inner.SentPackets);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        transport.Poll(new RecordingNetworkEventHandler());
        Assert.Single(inner.SentPackets);

        RecordingNetworkEventHandler handler = new();
        inner.EnqueuePacket(peerId, [2], NetworkDelivery.Sequenced, channel: 5);

        transport.Poll(handler);
        Assert.Empty(handler.Packets);

        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        transport.Poll(handler);

        ReceivedPacket received = Assert.Single(handler.Packets);
        Assert.Equal([2], received.Payload);
        Assert.Equal(5, received.Channel);
    }

    [Fact]
    public void DisconnectClearsDelayedOutboundSendsForPeer()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(latency: TimeSpan.FromMilliseconds(50)),
            timeProvider);
        NetworkPeerId disconnectedPeerId = new(10);
        NetworkPeerId connectedPeerId = new(11);

        transport.Send(disconnectedPeerId, [1], NetworkDelivery.ReliableOrdered, channel: 2);
        transport.Send(connectedPeerId, [2], NetworkDelivery.ReliableOrdered, channel: 3);
        Assert.Empty(inner.SentPackets);

        transport.Disconnect(disconnectedPeerId);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        transport.Poll(new RecordingNetworkEventHandler());

        Assert.Equal([disconnectedPeerId], inner.DisconnectedPeers);
        SentPacket sent = Assert.Single(inner.SentPackets);
        Assert.Equal(connectedPeerId, sent.PeerId);
        Assert.Equal([2], sent.Payload);
        Assert.Equal(3, sent.Channel);
    }

    [Fact]
    public void RemoteDisconnectClearsDelayedInboundReceivesForPeerBeforeForwardingDisconnect()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(latency: TimeSpan.FromMilliseconds(50)),
            timeProvider);
        RecordingNetworkEventHandler handler = new();
        NetworkPeerId peerId = new(12);

        inner.EnqueuePacket(peerId, [8], NetworkDelivery.Sequenced, channel: 4);
        transport.Poll(handler);
        Assert.Empty(handler.Packets);

        inner.EnqueueDisconnected(peerId, NetworkDisconnectReason.RemoteConnectionClose);
        transport.Poll(handler);
        Assert.Equal([peerId], handler.DisconnectedPeers);

        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        transport.Poll(handler);
        Assert.Empty(handler.Packets);
    }

    [Fact]
    public void JitterUsesDeterministicDueTimesWithFixedSeed()
    {
        List<int> firstRun = CaptureJitterReleaseTimes();
        List<int> secondRun = CaptureJitterReleaseTimes();

        Assert.Equal(firstRun, secondRun);
        Assert.True(firstRun.Distinct().Count() > 1);
    }

    [Fact]
    public void LossChanceOneDropsPackets()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(
                latency: TimeSpan.FromMilliseconds(10),
                lossChance: 1,
                randomSeed: 123),
            timeProvider);
        NetworkPeerId peerId = new(2);

        transport.Send(peerId, [1], NetworkDelivery.ReliableOrdered);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        transport.Poll(new RecordingNetworkEventHandler());
        Assert.Empty(inner.SentPackets);

        RecordingNetworkEventHandler handler = new();
        inner.EnqueuePacket(peerId, [2], NetworkDelivery.ReliableOrdered);
        transport.Poll(handler);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        transport.Poll(handler);

        Assert.Empty(handler.Packets);
    }

    [Fact]
    public void DuplicateChanceOneSendsAndDeliversTwoCopies()
    {
        FakeNetworkTransport inner = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(duplicateChance: 1, randomSeed: 42));
        NetworkPeerId peerId = new(3);

        transport.Send(peerId, [7], NetworkDelivery.Sequenced, channel: 1);

        Assert.Equal(2, inner.SentPackets.Count);
        Assert.All(inner.SentPackets, packet => Assert.Equal([7], packet.Payload));

        RecordingNetworkEventHandler handler = new();
        inner.EnqueuePacket(peerId, [8], NetworkDelivery.ReliableOrdered, channel: 6);

        transport.Poll(handler);

        Assert.Equal(2, handler.Packets.Count);
        Assert.All(handler.Packets, packet =>
        {
            Assert.Equal([8], packet.Payload);
            Assert.Equal(6, packet.Channel);
        });
    }

    [Fact]
    public void ReorderChanceOneReordersDuePacketsDeterministically()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(
                latency: TimeSpan.FromMilliseconds(10),
                reorderChance: 1,
                randomSeed: 11),
            timeProvider);
        RecordingNetworkEventHandler handler = new();
        NetworkPeerId peerId = new(4);

        inner.EnqueuePacket(peerId, [1], NetworkDelivery.Sequenced);
        inner.EnqueuePacket(peerId, [2], NetworkDelivery.Sequenced);
        inner.EnqueuePacket(peerId, [3], NetworkDelivery.Sequenced);

        transport.Poll(handler);
        Assert.Empty(handler.Packets);

        timeProvider.Advance(TimeSpan.FromMilliseconds(10));
        transport.Poll(handler);

        byte[] payloadOrder = handler.Packets.Select(packet => packet.Payload[0]).ToArray();
        Assert.Equal(3, payloadOrder.Length);
        Assert.NotEqual([1, 2, 3], payloadOrder);
        Assert.Equal(payloadOrder, CaptureReorderedInboundPayloads());
    }

    [Fact]
    public void ReorderChanceOneReordersOutboundDuePacketsDeterministically()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(
                latency: TimeSpan.FromMilliseconds(10),
                reorderChance: 1,
                randomSeed: 11),
            timeProvider);
        NetworkPeerId peerId = new(13);

        transport.Send(peerId, [1], NetworkDelivery.Sequenced);
        transport.Send(peerId, [2], NetworkDelivery.Sequenced);
        transport.Send(peerId, [3], NetworkDelivery.Sequenced);
        Assert.Empty(inner.SentPackets);

        timeProvider.Advance(TimeSpan.FromMilliseconds(10));
        transport.Poll(new RecordingNetworkEventHandler());

        byte[] payloadOrder = inner.SentPackets.Select(packet => packet.Payload[0]).ToArray();
        Assert.Equal([1, 2, 3], payloadOrder.Order().ToArray());
        Assert.NotEqual([1, 2, 3], payloadOrder);
        Assert.Equal(payloadOrder, CaptureReorderedOutboundPayloads());
    }

    [Fact]
    public void NonPacketEventsPassThroughImmediately()
    {
        FakeNetworkTransport inner = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(latency: TimeSpan.FromSeconds(1), randomSeed: 5));
        RecordingNetworkEventHandler handler = new();
        NetworkPeerId peerId = new(5);

        inner.EnqueueConnected(peerId, new NetworkEndpoint("127.0.0.1", 7777));
        inner.EnqueueDisconnected(peerId, NetworkDisconnectReason.RemoteConnectionClose);
        inner.EnqueueError(new NetworkEndpoint("127.0.0.1", 7777), SocketError.ConnectionReset);
        inner.EnqueueLatency(peerId, 123);
        inner.EnqueuePacket(peerId, [9], NetworkDelivery.ReliableOrdered);

        transport.Poll(handler);

        Assert.Equal([peerId], handler.ConnectedPeers);
        Assert.Equal([peerId], handler.DisconnectedPeers);
        Assert.Single(handler.Errors);
        Assert.Equal((peerId, 123), Assert.Single(handler.LatencyUpdates));
        Assert.Empty(handler.Packets);
    }

    [Fact]
    public void DisposeDisposesInnerTransportAndRejectsLaterOperations()
    {
        FakeNetworkTransport inner = new();
        SimulatedNetworkTransport transport = new(inner);

        transport.Dispose();

        Assert.True(inner.Disposed);
        Assert.Throws<ObjectDisposedException>(() => transport.Start(0));
        Assert.Throws<ObjectDisposedException>(() => transport.Connect(new NetworkEndpoint("127.0.0.1", 7777)));
        Assert.Throws<ObjectDisposedException>(
            () => transport.Send(new NetworkPeerId(0), [1], NetworkDelivery.ReliableOrdered));
        Assert.Throws<ObjectDisposedException>(() => transport.Disconnect(new NetworkPeerId(0)));
        Assert.Throws<ObjectDisposedException>(() => transport.Poll(new RecordingNetworkEventHandler()));
    }

    [Fact]
    public void CurrentConditionsReportsLiveReplacement()
    {
        FakeNetworkTransport inner = new();
        SimulatedNetworkConditions initial = new(latency: TimeSpan.FromMilliseconds(10), randomSeed: 7);
        SimulatedNetworkConditions replacement = new(lossChance: 0.25, randomSeed: 11);
        using SimulatedNetworkTransport transport = new(inner, initial);

        Assert.Same(initial, transport.CurrentConditions);

        transport.SetConditions(replacement);

        Assert.Same(replacement, transport.CurrentConditions);
    }

    [Fact]
    public void PeerStatisticsForwardToInnerDiagnosticsProvider()
    {
        NetworkPeerId peerId = new(17);
        NetworkPeerStatistics expected = new(
            OneWayLatencyMilliseconds: 12,
            RoundTripTimeMilliseconds: 24,
            MaximumTransmissionUnitBytes: 1200,
            TimeSinceLastPacketMilliseconds: 8,
            PacketsSent: 10,
            PacketsReceived: 11,
            BytesSent: 100,
            BytesReceived: 110,
            PacketsLost: 1,
            PacketLossPercent: 10);
        FakeNetworkTransport inner = new() { PeerStatistics = expected };
        using SimulatedNetworkTransport transport = new(inner);

        Assert.True(transport.TryGetPeerStatistics(peerId, out NetworkPeerStatistics actual));
        Assert.Equal(expected, actual);

        inner.PeerStatistics = null;
        Assert.False(transport.TryGetPeerStatistics(peerId, out _));
    }

    [Fact]
    public void ReapplyingConditionsWithSeedResetsRandomSequence()
    {
        FakeNetworkTransport inner = new();
        SimulatedNetworkConditions conditions = new(
            lossChance: 0.35,
            duplicateChance: 0.45,
            randomSeed: 12345);
        using SimulatedNetworkTransport transport = new(inner, conditions);
        NetworkPeerId peerId = new(20);

        for (byte payload = 0; payload < 20; payload++)
            transport.Send(peerId, [payload], NetworkDelivery.Sequenced);

        byte[] firstRun = inner.SentPackets.Select(packet => packet.Payload[0]).ToArray();
        inner.SentPackets.Clear();

        transport.SetConditions(conditions);
        for (byte payload = 0; payload < 20; payload++)
            transport.Send(peerId, [payload], NetworkDelivery.Sequenced);

        byte[] secondRun = inner.SentPackets.Select(packet => packet.Payload[0]).ToArray();

        Assert.NotEmpty(firstRun);
        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public void LiveReplacementAffectsNewPacketsButPreservesQueuedPacketDecisions()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(
                latency: TimeSpan.FromMilliseconds(50),
                duplicateChance: 1,
                reorderChance: 1,
                randomSeed: 99),
            timeProvider);
        NetworkPeerId peerId = new(21);

        transport.Send(peerId, [1], NetworkDelivery.Sequenced);
        transport.Send(peerId, [2], NetworkDelivery.Sequenced);
        transport.Send(peerId, [3], NetworkDelivery.Sequenced);

        transport.SetConditions(new SimulatedNetworkConditions(lossChance: 1, randomSeed: 99));
        transport.Send(peerId, [4], NetworkDelivery.Sequenced);

        timeProvider.Advance(TimeSpan.FromMilliseconds(49));
        transport.Poll(new RecordingNetworkEventHandler());
        Assert.Empty(inner.SentPackets);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        transport.Poll(new RecordingNetworkEventHandler());

        byte[] delivered = inner.SentPackets.Select(packet => packet.Payload[0]).ToArray();
        Assert.Equal(6, delivered.Length);
        Assert.Equal(2, delivered.Count(payload => payload == 1));
        Assert.Equal(2, delivered.Count(payload => payload == 2));
        Assert.Equal(2, delivered.Count(payload => payload == 3));
        Assert.DoesNotContain((byte)4, delivered);
        Assert.NotEqual([1, 1, 2, 2, 3, 3], delivered);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void ConditionsRejectInvalidProbabilities(double probability)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedNetworkConditions(lossChance: probability));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedNetworkConditions(duplicateChance: probability));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedNetworkConditions(reorderChance: probability));
    }

    [Fact]
    public void ConditionsRejectNegativeTiming()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulatedNetworkConditions(latency: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulatedNetworkConditions(jitter: TimeSpan.FromTicks(-1)));
    }

    private static List<int> CaptureJitterReleaseTimes()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(
                latency: TimeSpan.FromMilliseconds(10),
                jitter: TimeSpan.FromMilliseconds(10),
                randomSeed: 77),
            timeProvider);
        NetworkPeerId peerId = new(6);

        transport.Send(peerId, [1], NetworkDelivery.Sequenced);
        transport.Send(peerId, [2], NetworkDelivery.Sequenced);
        transport.Send(peerId, [3], NetworkDelivery.Sequenced);

        List<int> releaseTimes = [];
        for (int elapsedMs = 0; elapsedMs <= 25; elapsedMs++)
        {
            transport.Poll(new RecordingNetworkEventHandler());
            while (releaseTimes.Count < inner.SentPackets.Count)
            {
                releaseTimes.Add(elapsedMs);
            }

            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        }

        Assert.Equal(3, releaseTimes.Count);
        return releaseTimes;
    }

    private static byte[] CaptureReorderedInboundPayloads()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(
                latency: TimeSpan.FromMilliseconds(10),
                reorderChance: 1,
                randomSeed: 11),
            timeProvider);
        RecordingNetworkEventHandler handler = new();
        NetworkPeerId peerId = new(4);

        inner.EnqueuePacket(peerId, [1], NetworkDelivery.Sequenced);
        inner.EnqueuePacket(peerId, [2], NetworkDelivery.Sequenced);
        inner.EnqueuePacket(peerId, [3], NetworkDelivery.Sequenced);

        transport.Poll(handler);
        timeProvider.Advance(TimeSpan.FromMilliseconds(10));
        transport.Poll(handler);

        return handler.Packets.Select(packet => packet.Payload[0]).ToArray();
    }

    private static byte[] CaptureReorderedOutboundPayloads()
    {
        FakeNetworkTransport inner = new();
        ManualTimeProvider timeProvider = new();
        using SimulatedNetworkTransport transport = new(
            inner,
            new SimulatedNetworkConditions(
                latency: TimeSpan.FromMilliseconds(10),
                reorderChance: 1,
                randomSeed: 11),
            timeProvider);
        NetworkPeerId peerId = new(13);

        transport.Send(peerId, [1], NetworkDelivery.Sequenced);
        transport.Send(peerId, [2], NetworkDelivery.Sequenced);
        transport.Send(peerId, [3], NetworkDelivery.Sequenced);

        timeProvider.Advance(TimeSpan.FromMilliseconds(10));
        transport.Poll(new RecordingNetworkEventHandler());

        return inner.SentPackets.Select(packet => packet.Payload[0]).ToArray();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }

    private sealed class FakeNetworkTransport : INetworkTransport, INetworkTransportDiagnostics
    {
        private readonly Queue<Action<INetworkEventHandler>> _events = [];

        public List<SentPacket> SentPackets { get; } = [];

        public List<NetworkPeerId> DisconnectedPeers { get; } = [];

        public bool Disposed { get; private set; }

        public NetworkPeerStatistics? PeerStatistics { get; set; }

        public void Start(int port)
        {
            ThrowIfDisposed();
        }

        public NetworkPeerId Connect(NetworkEndpoint endpoint)
        {
            ThrowIfDisposed();
            return new NetworkPeerId(1);
        }

        public void Send(NetworkPeerId peerId, ReadOnlySpan<byte> packet, NetworkDelivery delivery, byte channel = 0)
        {
            ThrowIfDisposed();
            SentPackets.Add(new SentPacket(peerId, packet.ToArray(), delivery, channel));
        }

        public void Disconnect(NetworkPeerId peerId)
        {
            ThrowIfDisposed();
            DisconnectedPeers.Add(peerId);
        }

        public void Poll(INetworkEventHandler handler)
        {
            ThrowIfDisposed();
            while (_events.TryDequeue(out Action<INetworkEventHandler>? handleEvent))
            {
                handleEvent(handler);
            }
        }

        public void Dispose()
        {
            Disposed = true;
            _events.Clear();
        }

        public bool TryGetPeerStatistics(NetworkPeerId peerId, out NetworkPeerStatistics statistics)
        {
            ThrowIfDisposed();
            if (PeerStatistics is NetworkPeerStatistics available)
            {
                statistics = available;
                return true;
            }

            statistics = default;
            return false;
        }

        public void EnqueueConnected(NetworkPeerId peerId, NetworkEndpoint endpoint)
        {
            _events.Enqueue(handler => handler.Connected(peerId, endpoint));
        }

        public void EnqueueDisconnected(NetworkPeerId peerId, NetworkDisconnectReason reason)
        {
            _events.Enqueue(handler => handler.Disconnected(peerId, reason));
        }

        public void EnqueuePacket(
            NetworkPeerId peerId,
            byte[] packet,
            NetworkDelivery delivery,
            byte channel = 0)
        {
            byte[] packetCopy = packet.ToArray();
            _events.Enqueue(handler => handler.PacketReceived(peerId, packetCopy, delivery, channel));
        }

        public void EnqueueError(NetworkEndpoint? endpoint, SocketError socketError)
        {
            _events.Enqueue(handler => handler.NetworkError(endpoint, socketError));
        }

        public void EnqueueLatency(NetworkPeerId peerId, int latencyMilliseconds)
        {
            _events.Enqueue(handler => handler.LatencyUpdated(peerId, latencyMilliseconds));
        }

        private void ThrowIfDisposed()
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(nameof(FakeNetworkTransport));
            }
        }
    }

    private sealed record SentPacket(
        NetworkPeerId PeerId,
        byte[] Payload,
        NetworkDelivery Delivery,
        byte Channel);
}
