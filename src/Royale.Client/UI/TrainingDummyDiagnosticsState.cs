using System.Globalization;
using Royale.Client.Gameplay;

namespace Royale.Client.UI;

public readonly record struct TrainingDummyDiagnosticsState(
    string Id,
    int CurrentHealth,
    int MaxHealth,
    bool Alive,
    IReadOnlyList<TrainingDummyDamageEntry> DamageHistory)
{
    public string HealthText => $"Health: {CurrentHealth}/{MaxHealth}";

    public string AliveText => $"State: {(Alive ? "alive" : "dead")}";

    public string HistoryHeaderText => $"Recent damage ({DamageHistory.Count}/{TrainingDummy.DamageHistoryCapacity})";

    public static TrainingDummyDiagnosticsState FromDummy(TrainingDummy trainingDummy)
    {
        ArgumentNullException.ThrowIfNull(trainingDummy);

        return new TrainingDummyDiagnosticsState(
            trainingDummy.Id,
            trainingDummy.Health.CurrentHealth,
            trainingDummy.Health.MaxHealth,
            trainingDummy.Health.Alive,
            trainingDummy.DamageHistory);
    }

    public static string FormatDamageEntry(TrainingDummyDamageEntry entry)
    {
        string hitRegion = entry.HitRegion ?? "-";
        string falloff = entry.FalloffMultiplier?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        string random = entry.RandomModifier?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"tick {entry.Tick}: {entry.WeaponId} raw {entry.RawDamage} applied {entry.AppliedDamage} hp {entry.RemainingHealth} dist {entry.HitDistance:0.00} hit ({entry.HitPoint.X:0.00}, {entry.HitPoint.Y:0.00}, {entry.HitPoint.Z:0.00}) region {hitRegion} falloff {falloff} random {random}");
    }
}
