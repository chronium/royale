namespace Royale.Simulation.Combat;

public readonly record struct DamageResult(
    bool Applied,
    string? TargetId,
    int RawDamage,
    int AppliedDamage,
    int PreviousHealth,
    int CurrentHealth,
    bool Killed)
{
    public static DamageResult NoDamage(string? targetId, int rawDamage, int health = 0) =>
        new(
            Applied: false,
            targetId,
            rawDamage,
            AppliedDamage: 0,
            PreviousHealth: health,
            CurrentHealth: health,
            Killed: false);
}
