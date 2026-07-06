namespace Royale.Simulation;

public readonly record struct DamageRequest(string WeaponId, int RawDamage, HitscanHit Hit);
