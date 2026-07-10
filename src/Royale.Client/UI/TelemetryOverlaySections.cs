using System.Globalization;
using System.Numerics;
using System.Net.Sockets;
using Royale.Client.Networking;
using Royale.Network;
using Royale.Protocol;

namespace Royale.Client.UI;

public readonly record struct TelemetrySimulationState(
    ulong? ServerTick,
    long? ServerTickDifference,
    int? PendingInputCount,
    int? ReplayedInputCount,
    ulong? ReconciliationCount,
    float? CorrectionDistance);

public sealed record TelemetryPlayerState(string Status, TelemetryPlayerValues? Values, PlayerDiagnosticsState? OfflineDiagnostics)
{
    public bool Available => Values is not null;
}

public sealed record TelemetryPlayerValues(
    string Source,
    Vector3 Position,
    Vector3 Velocity,
    float YawRadians,
    float PitchRadians,
    int CurrentHealth,
    int MaxHealth,
    bool Alive,
    bool? Grounded,
    string? GroundedSource,
    string WeaponId,
    int? AmmoInMagazine,
    int? ReserveAmmo,
    int? MagazineCapacity)
{
    public string PositionText => $"Position: {FormatVector(Position)}";

    public string VelocityText => $"Velocity: {FormatVector(Velocity)}";

    public string LookText => string.Create(
        CultureInfo.InvariantCulture,
        $"Look: yaw {YawRadians:0.000}, pitch {PitchRadians:0.000} rad");

    public string HealthText => $"Health: {CurrentHealth}/{MaxHealth}";

    public string AliveText => $"State: {(Alive ? "alive" : "dead")}";

    public string GroundedText => Grounded is bool grounded
        ? $"Grounded: {(grounded ? "yes" : "no")}{(GroundedSource is null ? string.Empty : $" ({GroundedSource})")}"
        : "Grounded: unavailable in authoritative snapshots";

    public string WeaponText => $"Weapon: {WeaponId}";

    public string AmmunitionText => AmmoInMagazine is int magazine && ReserveAmmo is int reserve
        ? $"Ammunition: {magazine} / {reserve} reserve"
        : MagazineCapacity is int capacity
            ? $"Ammunition: not tracked offline (magazine capacity {capacity})"
            : "Ammunition: unavailable";

    private static string FormatVector(Vector3 value) => string.Create(
        CultureInfo.InvariantCulture,
        $"({value.X:0.00}, {value.Y:0.00}, {value.Z:0.00})");
}

public sealed record TelemetryPhysicsState(
    string Mode,
    bool? CollisionWorldAvailable,
    int? StaticColliderCount,
    bool? PredictionActive,
    bool? PredictionSeeded);

public sealed record TelemetryServerState(string Status, ServerSnapshot? Snapshot)
{
    public bool Available => Snapshot is not null;
}

public sealed record TelemetryNetworkState(
    ClientNetworkTelemetryValues Client,
    NetworkPeerStatistics? Transport,
    int RemoteSnapshotBufferCount,
    ulong RemoteInterpolationDelayTicks,
    double LastRemoteInterpolationTargetTick,
    bool LastRemoteRenderUsedInterpolation);

public readonly record struct ClientNetworkTelemetryValues(
    ulong SuccessfulInputSendCount,
    ulong ReceivedPacketCount,
    ulong ReceivedSnapshotPacketCount,
    ulong ValidSnapshotPacketCount,
    ulong InvalidSnapshotPacketCount,
    ulong NetworkErrorCount,
    ulong LatencySampleCount,
    int? OneWayLatencyMilliseconds,
    double? LatencyJitterMilliseconds)
{
    public static ClientNetworkTelemetryValues FromDiagnostics(ClientNetworkDiagnostics diagnostics) => new(
        diagnostics.SuccessfulInputSendCount,
        diagnostics.ReceivedPacketCount,
        diagnostics.ReceivedSnapshotPacketCount,
        diagnostics.ValidSnapshotPacketCount,
        diagnostics.InvalidSnapshotPacketCount,
        diagnostics.NetworkErrorCount,
        diagnostics.LatencySampleCount,
        diagnostics.OneWayLatencyMilliseconds,
        diagnostics.LatencyJitterMilliseconds);
}

public sealed record TelemetryConnectionState(
    string Mode,
    NetworkEndpoint? Endpoint,
    NetworkPeerId? PeerId,
    string Status,
    NetworkHandshakeClientState? HandshakeState,
    ServerAccept? AcceptedSession,
    ServerReject? Rejection,
    NetworkDisconnectReason? LastDisconnectReason,
    ClientNetworkErrorValues? LastNetworkError);

public readonly record struct ClientNetworkErrorValues(NetworkEndpoint? Endpoint, SocketError SocketError);
