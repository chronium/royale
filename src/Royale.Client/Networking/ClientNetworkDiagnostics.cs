using System.Net.Sockets;
using Royale.Network.Handshake;
using Royale.Network.Input;
using Royale.Network.Simulation;
using Royale.Network.Snapshots;
using Royale.Network.Transport;

namespace Royale.Client.Networking;

public sealed class ClientNetworkDiagnostics
{
    private int? previousLatencyMilliseconds;

    public ulong SuccessfulInputSendCount { get; private set; }

    public ulong ReceivedPacketCount { get; private set; }

    public ulong ReceivedSnapshotPacketCount { get; private set; }

    public ulong ValidSnapshotPacketCount { get; private set; }

    public ulong InvalidSnapshotPacketCount { get; private set; }

    public ulong NetworkErrorCount { get; private set; }

    public ulong LatencySampleCount { get; private set; }

    public int? OneWayLatencyMilliseconds { get; private set; }

    public double? LatencyJitterMilliseconds { get; private set; }

    public NetworkDisconnectReason? LastDisconnectReason { get; private set; }

    public ClientNetworkError? LastNetworkError { get; private set; }

    internal void RecordSuccessfulInputSend() => SuccessfulInputSendCount++;

    internal void RecordPacketReceived() => ReceivedPacketCount++;

    internal void RecordSnapshotPacket(bool valid)
    {
        ReceivedSnapshotPacketCount++;

        if (valid)
            ValidSnapshotPacketCount++;
        else
            InvalidSnapshotPacketCount++;
    }

    internal void RecordNetworkError(NetworkEndpoint? endpoint, SocketError socketError)
    {
        NetworkErrorCount++;
        LastNetworkError = new ClientNetworkError(endpoint, socketError);
    }

    internal void RecordDisconnect(NetworkDisconnectReason reason) => LastDisconnectReason = reason;

    internal void RecordLatency(int latencyMilliseconds)
    {
        int normalizedLatency = Math.Max(0, latencyMilliseconds);

        if (previousLatencyMilliseconds is int previousLatency)
        {
            double difference = Math.Abs(normalizedLatency - previousLatency);
            LatencyJitterMilliseconds = LatencyJitterMilliseconds is double currentJitter
                ? currentJitter + (difference - currentJitter) / 16.0
                : difference / 16.0;
        }

        previousLatencyMilliseconds = normalizedLatency;
        OneWayLatencyMilliseconds = normalizedLatency;
        LatencySampleCount++;
    }
}

public readonly record struct ClientNetworkError(NetworkEndpoint? Endpoint, SocketError SocketError);
