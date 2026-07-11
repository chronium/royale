using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Simulation.Tests.Combat;

public sealed class WeaponFireControllerTests
{
    [Fact]
    public void DefaultRifleCadenceResolvesToSixTicksAtSimulationTickRate()
    {
        int intervalTicks = WeaponFireController.ResolveFireIntervalTicks(WeaponCatalog.DefaultRifle);

        Assert.Equal(6, intervalTicks);
    }

    [Fact]
    public void FirstHeldFireTickFiresImmediately()
    {
        WeaponFireStepResult result = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            WeaponFireState.Ready,
            fireHeld: true,
            currentTick: 0);

        Assert.True(result.Fired);
        Assert.Equal((ulong)0, result.State.LastFiredTick);
        Assert.Equal((ulong)6, result.State.NextAllowedFireTick);
    }

    [Fact]
    public void HeldFireIsBlockedDuringCooldownAndFiresOnNextEligibleTick()
    {
        WeaponFireStepResult first = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            WeaponFireState.Ready,
            fireHeld: true,
            currentTick: 0);

        WeaponFireStepResult blocked = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            first.State,
            fireHeld: true,
            currentTick: 5);

        WeaponFireStepResult eligible = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            blocked.State,
            fireHeld: true,
            currentTick: 6);

        Assert.False(blocked.Fired);
        Assert.Equal(first.State, blocked.State);
        Assert.True(eligible.Fired);
        Assert.Equal((ulong)6, eligible.State.LastFiredTick);
        Assert.Equal((ulong)12, eligible.State.NextAllowedFireTick);
    }

    [Fact]
    public void ReleasingFireDoesNotProduceShotsOrBypassCooldown()
    {
        WeaponFireStepResult first = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            WeaponFireState.Ready,
            fireHeld: true,
            currentTick: 0);

        WeaponFireStepResult released = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            first.State,
            fireHeld: false,
            currentTick: 6);

        WeaponFireStepResult pressedBeforeCooldownEnds = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            first.State,
            fireHeld: true,
            currentTick: 5);

        WeaponFireStepResult pressedAfterCooldownEnds = WeaponFireController.Step(
            WeaponCatalog.DefaultRifle,
            released.State,
            fireHeld: true,
            currentTick: 6);

        Assert.False(released.Fired);
        Assert.Equal(first.State, released.State);
        Assert.False(pressedBeforeCooldownEnds.Fired);
        Assert.True(pressedAfterCooldownEnds.Fired);
    }
}
