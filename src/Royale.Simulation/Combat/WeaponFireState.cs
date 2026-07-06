namespace Royale.Simulation.Combat;

public readonly record struct WeaponFireState(ulong NextAllowedFireTick, ulong? LastFiredTick)
{
    public static WeaponFireState Ready { get; } = new(0, null);
}
