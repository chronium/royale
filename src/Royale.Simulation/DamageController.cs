using Royale.Content;

namespace Royale.Simulation;

public static class DamageController
{
    public static DamageResult Apply(
        WeaponDefinition weapon,
        HitscanHit hit,
        IDictionary<string, HealthState> targetHealth)
    {
        ArgumentNullException.ThrowIfNull(weapon);

        return Apply(new DamageRequest(weapon.Id, weapon.Damage, hit), targetHealth);
    }

    public static DamageResult Apply(
        DamageRequest request,
        IDictionary<string, HealthState> targetHealth)
    {
        ArgumentNullException.ThrowIfNull(targetHealth);

        if (!request.Hit.IsTarget || string.IsNullOrWhiteSpace(request.Hit.TargetId))
            return DamageResult.NoDamage(request.Hit.TargetId, request.RawDamage);

        string targetId = request.Hit.TargetId;
        if (!targetHealth.TryGetValue(targetId, out HealthState previous))
            return DamageResult.NoDamage(targetId, request.RawDamage);

        if (!previous.Alive || previous.CurrentHealth <= 0 || request.RawDamage <= 0)
            return DamageResult.NoDamage(targetId, request.RawDamage, previous.CurrentHealth);

        HealthState current = previous.ApplyDamage(request.RawDamage);
        targetHealth[targetId] = current;

        int appliedDamage = previous.CurrentHealth - current.CurrentHealth;
        return new DamageResult(
            Applied: appliedDamage > 0,
            targetId,
            request.RawDamage,
            appliedDamage,
            previous.CurrentHealth,
            current.CurrentHealth,
            Killed: previous.Alive && !current.Alive);
    }
}
