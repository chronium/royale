namespace Royale.Simulation;

public readonly record struct WeaponFireStepResult(bool Fired, WeaponFireState State, int FireIntervalTicks);
