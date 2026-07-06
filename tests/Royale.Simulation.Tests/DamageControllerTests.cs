using System.Numerics;
using Royale.Content;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Simulation.Tests;

public sealed class DamageControllerTests
{
    [Fact]
    public void DefaultPlayerHealthStartsFullAndAlive()
    {
        HealthState health = HealthState.DefaultPlayer;

        Assert.Equal(100, health.MaxHealth);
        Assert.Equal(100, health.CurrentHealth);
        Assert.True(health.Alive);
    }

    [Fact]
    public void RifleTargetHitAppliesRifleDamage()
    {
        Dictionary<string, HealthState> health = CreateHealthTable("target-a");

        DamageResult result = DamageController.Apply(WeaponCatalog.DefaultRifle, TargetHit("target-a"), health);

        Assert.True(result.Applied);
        Assert.Equal("target-a", result.TargetId);
        Assert.Equal(25, result.RawDamage);
        Assert.Equal(25, result.AppliedDamage);
        Assert.Equal(100, result.PreviousHealth);
        Assert.Equal(75, result.CurrentHealth);
        Assert.False(result.Killed);
        Assert.Equal(75, health["target-a"].CurrentHealth);
        Assert.True(health["target-a"].Alive);
    }

    [Fact]
    public void FourRifleTargetHitsReduceHealthToZeroAndMarkDead()
    {
        Dictionary<string, HealthState> health = CreateHealthTable("target-a");
        DamageResult result = default;

        for (int i = 0; i < 4; i++)
            result = DamageController.Apply(WeaponCatalog.DefaultRifle, TargetHit("target-a"), health);

        Assert.True(result.Applied);
        Assert.True(result.Killed);
        Assert.Equal(25, result.PreviousHealth);
        Assert.Equal(0, result.CurrentHealth);
        Assert.Equal(0, health["target-a"].CurrentHealth);
        Assert.False(health["target-a"].Alive);
    }

    [Fact]
    public void DamageClampsAtZero()
    {
        Dictionary<string, HealthState> health = new(StringComparer.Ordinal)
        {
            ["target-a"] = new HealthState(100, 10, alive: true),
        };

        DamageResult result = DamageController.Apply(WeaponCatalog.DefaultRifle, TargetHit("target-a"), health);

        Assert.True(result.Applied);
        Assert.Equal(10, result.AppliedDamage);
        Assert.Equal(0, result.CurrentHealth);
        Assert.True(result.Killed);
        Assert.Equal(0, health["target-a"].CurrentHealth);
    }

    [Fact]
    public void StaticHitsAndNoHitApplyNoDamage()
    {
        Dictionary<string, HealthState> health = CreateHealthTable("target-a");

        DamageResult staticResult = DamageController.Apply(WeaponCatalog.DefaultRifle, StaticHit(), health);
        DamageResult noHitResult = DamageController.Apply(WeaponCatalog.DefaultRifle, HitscanHit.None, health);

        Assert.False(staticResult.Applied);
        Assert.False(noHitResult.Applied);
        Assert.Null(staticResult.TargetId);
        Assert.Null(noHitResult.TargetId);
        Assert.Equal(100, health["target-a"].CurrentHealth);
        Assert.True(health["target-a"].Alive);
    }

    [Fact]
    public void TargetHitWithMissingTargetIdAppliesNoDamageWithClearResult()
    {
        Dictionary<string, HealthState> health = CreateHealthTable("target-a");

        DamageResult result = DamageController.Apply(WeaponCatalog.DefaultRifle, TargetHit(targetId: null), health);

        Assert.False(result.Applied);
        Assert.Null(result.TargetId);
        Assert.Equal(25, result.RawDamage);
        Assert.Equal(0, result.AppliedDamage);
        Assert.Equal(0, result.PreviousHealth);
        Assert.Equal(0, result.CurrentHealth);
        Assert.False(result.Killed);
        Assert.Equal(100, health["target-a"].CurrentHealth);
    }

    [Fact]
    public void MissingTargetHealthEntryAppliesNoDamageWithClearResult()
    {
        Dictionary<string, HealthState> health = CreateHealthTable("target-a");

        DamageResult result = DamageController.Apply(WeaponCatalog.DefaultRifle, TargetHit("missing-target"), health);

        Assert.False(result.Applied);
        Assert.Equal("missing-target", result.TargetId);
        Assert.Equal(25, result.RawDamage);
        Assert.Equal(0, result.AppliedDamage);
        Assert.Equal(0, result.PreviousHealth);
        Assert.Equal(0, result.CurrentHealth);
        Assert.False(result.Killed);
        Assert.Equal(100, health["target-a"].CurrentHealth);
    }

    [Fact]
    public void AlreadyDeadTargetReceivesNoFurtherDamage()
    {
        Dictionary<string, HealthState> health = new(StringComparer.Ordinal)
        {
            ["target-a"] = new HealthState(100, 0, alive: false),
        };

        DamageResult result = DamageController.Apply(WeaponCatalog.DefaultRifle, TargetHit("target-a"), health);

        Assert.False(result.Applied);
        Assert.Equal("target-a", result.TargetId);
        Assert.Equal(0, result.AppliedDamage);
        Assert.Equal(0, result.PreviousHealth);
        Assert.Equal(0, result.CurrentHealth);
        Assert.False(result.Killed);
        Assert.Equal(0, health["target-a"].CurrentHealth);
        Assert.False(health["target-a"].Alive);
    }

    private static Dictionary<string, HealthState> CreateHealthTable(string targetId) =>
        new(StringComparer.Ordinal)
        {
            [targetId] = HealthState.DefaultPlayer,
        };

    private static HitscanHit TargetHit(string? targetId) => new(
        HitscanHitType.Target,
        new Vector3(0.0f, 1.0f, -10.0f),
        Vector3.UnitZ,
        Distance: 10.0f,
        Fraction: 0.1f,
        StaticCollider: null,
        targetId);

    private static HitscanHit StaticHit() => new(
        HitscanHitType.Static,
        new Vector3(0.0f, 1.0f, -5.0f),
        Vector3.UnitZ,
        Distance: 5.0f,
        Fraction: 0.05f,
        StaticCollider: null,
        TargetId: null);
}
