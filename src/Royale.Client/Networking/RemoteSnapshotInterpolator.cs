using System.Numerics;
using Royale.Protocol;

namespace Royale.Client.Networking;

public sealed class RemoteSnapshotInterpolator
{
    public const ulong DefaultInterpolationDelayTicks = 6;
    public const int DefaultCapacity = 32;
    private const double ServerTickRate = 60.0;
    private const double ServerTickSeconds = 1.0 / ServerTickRate;

    private readonly List<ServerSnapshot> snapshots;
    private readonly int capacity;
    private double presentationServerTick;
    private bool clockInitialized;

    public RemoteSnapshotInterpolator(
        ulong interpolationDelayTicks = DefaultInterpolationDelayTicks,
        int capacity = DefaultCapacity)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must allow at least two snapshots.");

        InterpolationDelayTicks = interpolationDelayTicks;
        this.capacity = capacity;
        snapshots = new List<ServerSnapshot>(capacity);
    }

    public ulong InterpolationDelayTicks { get; }

    public int BufferedSnapshotCount => snapshots.Count;

    public double LastInterpolationTargetTick { get; private set; }

    public bool LastRenderUsedInterpolation { get; private set; }

    public void AddSnapshot(ServerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!clockInitialized)
        {
            presentationServerTick = snapshot.ServerTick;
            clockInitialized = true;
        }

        int existingIndex = snapshots.FindIndex(candidate => candidate.ServerTick == snapshot.ServerTick);
        if (existingIndex >= 0)
        {
            snapshots[existingIndex] = snapshot;
            return;
        }

        int insertIndex = snapshots.FindIndex(candidate => candidate.ServerTick > snapshot.ServerTick);
        if (insertIndex < 0)
            snapshots.Add(snapshot);
        else
            snapshots.Insert(insertIndex, snapshot);

        while (snapshots.Count > capacity)
            snapshots.RemoveAt(0);
    }

    public void Advance(double deltaSeconds)
    {
        if (!clockInitialized ||
            !double.IsFinite(deltaSeconds) ||
            deltaSeconds <= 0.0)
        {
            return;
        }

        presentationServerTick += deltaSeconds / ServerTickSeconds;
    }

    public ServerSnapshot? CreatePresentationSnapshot(ServerSnapshot? latestAuthoritativeSnapshot)
    {
        if (latestAuthoritativeSnapshot is null)
        {
            LastRenderUsedInterpolation = false;
            return null;
        }

        if (snapshots.Count == 0)
        {
            LastInterpolationTargetTick = latestAuthoritativeSnapshot.ServerTick;
            LastRenderUsedInterpolation = false;
            return latestAuthoritativeSnapshot;
        }

        double targetTick = clockInitialized
            ? presentationServerTick - InterpolationDelayTicks
            : latestAuthoritativeSnapshot.ServerTick;
        LastInterpolationTargetTick = targetTick;

        if (snapshots.Count < 2)
        {
            LastRenderUsedInterpolation = false;
            return latestAuthoritativeSnapshot;
        }

        bool hasBracket = TryFindBracket(targetTick, out ServerSnapshot? older, out ServerSnapshot? newer);
        bool usedInterpolation = false;
        PlayerSnapshotState[] players = latestAuthoritativeSnapshot.Players.ToArray();

        for (int i = 0; i < players.Length; i++)
        {
            PlayerSnapshotState latestPlayer = players[i];
            if (latestAuthoritativeSnapshot.LocalPlayerId == latestPlayer.PlayerId)
                continue;

            PlayerSnapshotState? transformSample = null;
            if (hasBracket &&
                older is not null &&
                newer is not null &&
                TryFindPlayer(older, latestPlayer.PlayerId, out PlayerSnapshotState olderPlayer) &&
                TryFindPlayer(newer, latestPlayer.PlayerId, out PlayerSnapshotState newerPlayer))
            {
                double range = newer.ServerTick - older.ServerTick;
                float amount = range <= 0.0
                    ? 0.0f
                    : (float)Math.Clamp((targetTick - older.ServerTick) / range, 0.0, 1.0);
                transformSample = InterpolatePlayerTransform(olderPlayer, newerPlayer, amount);
                usedInterpolation = true;
            }
            else if (TryFindNearestPlayer(latestPlayer.PlayerId, targetTick, out PlayerSnapshotState nearestPlayer))
            {
                transformSample = nearestPlayer;
            }

            if (transformSample is PlayerSnapshotState transform)
                players[i] = CopyTransform(latestPlayer, transform);
        }

        LastRenderUsedInterpolation = usedInterpolation;
        return usedInterpolation || !PlayersEqual(latestAuthoritativeSnapshot.Players, players)
            ? latestAuthoritativeSnapshot with { Players = players }
            : latestAuthoritativeSnapshot;
    }

    public void Reset()
    {
        snapshots.Clear();
        presentationServerTick = 0.0;
        LastInterpolationTargetTick = 0.0;
        LastRenderUsedInterpolation = false;
        clockInitialized = false;
    }

    private bool TryFindBracket(
        double targetTick,
        out ServerSnapshot? older,
        out ServerSnapshot? newer)
    {
        older = null;
        newer = null;

        foreach (ServerSnapshot snapshot in snapshots)
        {
            if (snapshot.ServerTick <= targetTick)
                older = snapshot;

            if (snapshot.ServerTick >= targetTick)
            {
                newer = snapshot;
                break;
            }
        }

        if (older is null || newer is null)
            return false;

        if (older.ServerTick == newer.ServerTick)
        {
            int olderIndex = snapshots.IndexOf(older);
            if (olderIndex + 1 < snapshots.Count)
                newer = snapshots[olderIndex + 1];
            else if (olderIndex > 0)
                older = snapshots[olderIndex - 1];
        }

        return older.ServerTick != newer.ServerTick;
    }

    private bool TryFindNearestPlayer(
        uint playerId,
        double targetTick,
        out PlayerSnapshotState player)
    {
        ServerSnapshot? nearestSnapshot = null;
        double nearestDistance = double.MaxValue;

        foreach (ServerSnapshot snapshot in snapshots)
        {
            if (!TryFindPlayer(snapshot, playerId, out _))
                continue;

            double distance = Math.Abs(snapshot.ServerTick - targetTick);
            if (distance < nearestDistance ||
                (Math.Abs(distance - nearestDistance) <= double.Epsilon &&
                    nearestSnapshot is not null &&
                    snapshot.ServerTick > nearestSnapshot.ServerTick))
            {
                nearestSnapshot = snapshot;
                nearestDistance = distance;
            }
        }

        if (nearestSnapshot is not null &&
            TryFindPlayer(nearestSnapshot, playerId, out player))
        {
            return true;
        }

        player = default;
        return false;
    }

    private static bool TryFindPlayer(ServerSnapshot snapshot, uint playerId, out PlayerSnapshotState player)
    {
        foreach (PlayerSnapshotState candidate in snapshot.Players)
        {
            if (candidate.PlayerId == playerId)
            {
                player = candidate;
                return true;
            }
        }

        player = default;
        return false;
    }

    private static PlayerSnapshotState InterpolatePlayerTransform(
        PlayerSnapshotState older,
        PlayerSnapshotState newer,
        float amount) => newer with
        {
            Position = Vector3.Lerp(older.Position, newer.Position, amount),
            Velocity = Vector3.Lerp(older.Velocity, newer.Velocity, amount),
            YawRadians = InterpolateAngleRadians(older.YawRadians, newer.YawRadians, amount),
            PitchRadians = older.PitchRadians + (newer.PitchRadians - older.PitchRadians) * amount,
            Crouched = amount < 0.5f ? older.Crouched : newer.Crouched,
            Sprinting = amount < 0.5f ? older.Sprinting : newer.Sprinting,
        };

    private static PlayerSnapshotState CopyTransform(
        PlayerSnapshotState latestAuthoritativePlayer,
        PlayerSnapshotState transformSample) => latestAuthoritativePlayer with
        {
            Position = transformSample.Position,
            Velocity = transformSample.Velocity,
            YawRadians = transformSample.YawRadians,
            PitchRadians = transformSample.PitchRadians,
            Crouched = transformSample.Crouched,
            Sprinting = transformSample.Sprinting,
        };

    private static float InterpolateAngleRadians(float older, float newer, float amount)
    {
        float delta = MathF.IEEERemainder(newer - older, MathF.Tau);
        return NormalizeAngleRadians(older + delta * amount);
    }

    private static float NormalizeAngleRadians(float angle)
    {
        float normalized = MathF.IEEERemainder(angle, MathF.Tau);
        if (normalized <= -MathF.PI)
            normalized += MathF.Tau;
        else if (normalized > MathF.PI)
            normalized -= MathF.Tau;

        return normalized;
    }

    private static bool PlayersEqual(IReadOnlyList<PlayerSnapshotState> left, IReadOnlyList<PlayerSnapshotState> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i]))
                return false;
        }

        return true;
    }
}
