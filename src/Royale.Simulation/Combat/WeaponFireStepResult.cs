namespace Royale.Simulation.Combat;

public readonly record struct WeaponFireStepResult(bool Fired, WeaponFireState State, int FireIntervalTicks);
