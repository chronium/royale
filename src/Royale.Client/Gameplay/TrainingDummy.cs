using System.Numerics;
using Royale.Content;
using Royale.Simulation;

namespace Royale.Client.Gameplay;

public sealed class TrainingDummy
{
    public const string StableId = "training-dummy";
    public const int DamageHistoryCapacity = 16;

    private static readonly KinematicCharacterSettings TargetSettings = new();

    private readonly Dictionary<string, HealthState> healthByTarget = new(StringComparer.Ordinal);
    private readonly List<TrainingDummyDamageEntry> damageHistory = [];

    public TrainingDummy(Vector3 feetPosition)
        : this(feetPosition, TargetSettings.Radius, TargetSettings.Height)
    {
    }

    public TrainingDummy(Vector3 feetPosition, float radius, float height)
    {
        if (!IsFinite(feetPosition))
            throw new ArgumentOutOfRangeException(nameof(feetPosition), "Training dummy position must be finite.");

        if (!float.IsFinite(radius) || radius <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Training dummy radius must be finite and positive.");

        if (!float.IsFinite(height) || height < radius * 2.0f)
            throw new ArgumentOutOfRangeException(nameof(height), "Training dummy height must be finite and at least two radii.");

        FeetPosition = feetPosition;
        Radius = radius;
        Height = height;
        healthByTarget[StableId] = HealthState.DefaultPlayer;
    }

    public string Id => StableId;

    public Vector3 FeetPosition { get; }

    public float Radius { get; }

    public float Height { get; }

    public HealthState Health => healthByTarget[StableId];

    public HitscanTarget Target => new(StableId, FeetPosition, Radius, Height);

    public IReadOnlyList<TrainingDummyDamageEntry> DamageHistory => damageHistory;

    public static TrainingDummy CreateDefault(Vector3 spawnFeetPosition) =>
        new(spawnFeetPosition + new Vector3(0.0f, 0.0f, 1.8f));

    public DamageResult ApplyDamage(WeaponDefinition weapon, HitscanHit hit, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return ApplyDamage(new DamageRequest(weapon.Id, weapon.Damage, hit), tick);
    }

    public DamageResult ApplyDamage(DamageRequest request, ulong tick)
    {
        DamageResult result = DamageController.Apply(request, healthByTarget);
        if (!result.Applied)
            return result;

        damageHistory.Insert(
            0,
            new TrainingDummyDamageEntry(
                tick,
                request.WeaponId,
                request.RawDamage,
                result.AppliedDamage,
                result.CurrentHealth,
                request.Hit.Distance,
                request.Hit.Point,
                HitRegion: null,
                FalloffMultiplier: null,
                RandomModifier: null));

        if (damageHistory.Count > DamageHistoryCapacity)
            damageHistory.RemoveAt(DamageHistoryCapacity);

        return result;
    }

    public void Reset()
    {
        healthByTarget[StableId] = HealthState.DefaultPlayer;
        damageHistory.Clear();
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
}
