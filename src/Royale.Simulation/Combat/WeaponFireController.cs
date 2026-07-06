using Royale.Content;
using Royale.Simulation.World;

namespace Royale.Simulation.Combat;

public static class WeaponFireController
{
    private const double IntegralTickTolerance = 0.000000001;

    public static int ResolveFireIntervalTicks(
        WeaponDefinition weapon,
        int tickRateHz = SimulationSettings.TickRateHz)
    {
        ArgumentNullException.ThrowIfNull(weapon);

        if (tickRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(tickRateHz), "Tick rate must be positive.");

        double intervalSeconds = weapon.FireInterval.TotalSeconds;

        if (!double.IsFinite(intervalSeconds) || intervalSeconds <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(weapon), "Weapon fire interval must be finite and positive.");

        double rawTicks = intervalSeconds * tickRateHz;
        double nearestIntegerTicks = Math.Round(rawTicks);
        double resolvedTicks = Math.Abs(rawTicks - nearestIntegerTicks) <= IntegralTickTolerance
            ? nearestIntegerTicks
            : Math.Ceiling(rawTicks);

        if (resolvedTicks <= 0.0 || resolvedTicks > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(weapon), "Weapon fire interval resolves outside the supported tick range.");

        return (int)resolvedTicks;
    }

    public static WeaponFireStepResult Step(
        WeaponDefinition weapon,
        WeaponFireState state,
        bool fireHeld,
        ulong currentTick,
        int tickRateHz = SimulationSettings.TickRateHz)
    {
        int intervalTicks = ResolveFireIntervalTicks(weapon, tickRateHz);

        if (!fireHeld || currentTick < state.NextAllowedFireTick)
            return new WeaponFireStepResult(Fired: false, state, intervalTicks);

        WeaponFireState nextState = state with
        {
            LastFiredTick = currentTick,
            NextAllowedFireTick = currentTick + (ulong)intervalTicks,
        };

        return new WeaponFireStepResult(Fired: true, nextState, intervalTicks);
    }
}
