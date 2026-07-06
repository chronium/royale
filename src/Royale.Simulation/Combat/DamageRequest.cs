namespace Royale.Simulation.Combat;

public readonly record struct DamageRequest(string WeaponId, int RawDamage, HitscanHit Hit);
