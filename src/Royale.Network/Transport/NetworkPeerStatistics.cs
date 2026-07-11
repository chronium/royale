namespace Royale.Network.Transport;

public readonly record struct NetworkPeerStatistics(
    int OneWayLatencyMilliseconds,
    int RoundTripTimeMilliseconds,
    int MaximumTransmissionUnitBytes,
    float TimeSinceLastPacketMilliseconds,
    long PacketsSent,
    long PacketsReceived,
    long BytesSent,
    long BytesReceived,
    long PacketsLost,
    long PacketLossPercent);
