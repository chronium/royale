using Royale.Content;
using Royale.Server;
using Royale.Simulation.Combat;

namespace Royale.Server.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class HeadlessServerSimulationTests
{
    [Fact]
    public void CreateLoadsDefaultGrayboxStaticCollisionWorld()
    {
        GameMap map = MapCatalog.LoadDefault();

        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Equal(ContentCatalog.DefaultMapId, simulation.MapId);
        Assert.Equal(map.StaticBoxes.Count, simulation.StaticColliderCount);
        Assert.Equal(0UL, simulation.CurrentTick);
        Assert.False(simulation.IsDisposed);
    }

    [Fact]
    public void CreateInitializesEmptyAuthoritativeState()
    {
        GameMap map = MapCatalog.LoadDefault();

        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Empty(simulation.Players);
        Assert.Equal(MatchPhase.WaitingForPlayers, simulation.MatchState.Phase);
        Assert.Equal(0UL, simulation.MatchState.PhaseStartedTick);
        Assert.Equal(0, simulation.MatchState.LivingPlayerCount);
        Assert.Null(simulation.MatchState.WinnerPlayerId);
        Assert.Equal(MapStaticBoxTransforms.ToVector3(map.SafeZone.Center), simulation.SafeZoneState.Center);
        Assert.Equal(map.SafeZone.Radius, simulation.SafeZoneState.CurrentRadius);
        Assert.Equal(map.SafeZone.Radius, simulation.SafeZoneState.TargetRadius);
        Assert.Equal(0UL, simulation.SafeZoneState.LastUpdatedTick);
    }

    [Fact]
    public void AddPlayerCreatesAliveAuthoritativePlayerState()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        AuthoritativePlayerState player = simulation.AddPlayer(new ServerConnectionId(42));

        Assert.Equal(new ServerPlayerId(1), player.PlayerId);
        Assert.Equal(new ServerConnectionId(42), player.ConnectionId);
        Assert.Equal(HealthState.DefaultPlayer, player.Health);
        Assert.True(player.Health.Alive);
        Assert.Equal(WeaponCatalog.DefaultRifle.Id, player.Weapon.WeaponId);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize, player.Weapon.AmmoInMagazine);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize * 3, player.Weapon.ReserveAmmo);
        Assert.Equal(WeaponFireState.Ready, player.Weapon.Fire);
        Assert.False(player.Weapon.IsReloading);
        Assert.Null(player.Weapon.ReloadCompleteTick);
        Assert.Null(player.LastProcessedInputSequence);
        Assert.True(player.Character.IsGrounded);
        AssertFinite(player);
        Assert.Single(simulation.Players);
        Assert.Equal(1, simulation.MatchState.LivingPlayerCount);
    }

    [Fact]
    public void AddPlayerAssignsUniqueIdsAndNonOverlappingSpawnReservations()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        AuthoritativePlayerState first = simulation.AddPlayer();
        AuthoritativePlayerState second = simulation.AddPlayer();
        AuthoritativePlayerState third = simulation.AddPlayer();

        Assert.Equal(new ServerPlayerId(1), first.PlayerId);
        Assert.Equal(new ServerPlayerId(2), second.PlayerId);
        Assert.Equal(new ServerPlayerId(3), third.PlayerId);
        Assert.False(first.SpawnReservation.Overlaps(second.SpawnReservation));
        Assert.False(first.SpawnReservation.Overlaps(third.SpawnReservation));
        Assert.False(second.SpawnReservation.Overlaps(third.SpawnReservation));
        Assert.Equal(3, simulation.Players.Count);
        Assert.Equal(3, simulation.MatchState.LivingPlayerCount);
    }

    [Fact]
    public void TryGetPlayerAndRemovePlayerUseAuthoritativeIds()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddPlayer();

        Assert.True(simulation.TryGetPlayer(player.PlayerId, out AuthoritativePlayerState? found));
        Assert.Same(player, found);

        Assert.True(simulation.RemovePlayer(player.PlayerId));
        Assert.False(simulation.TryGetPlayer(player.PlayerId, out AuthoritativePlayerState? removed));
        Assert.Null(removed);
        Assert.Empty(simulation.Players);
        Assert.Equal(0, simulation.MatchState.LivingPlayerCount);
    }

    [Fact]
    public void RemoveMissingPlayerReturnsFalse()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.False(simulation.RemovePlayer(new ServerPlayerId(999)));
    }

    [Fact]
    public void DisposePreventsAuthoritativeStateMutations()
    {
        HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        simulation.Dispose();

        Assert.Throws<ObjectDisposedException>(() => simulation.AddPlayer());
        Assert.Throws<ObjectDisposedException>(() => simulation.RemovePlayer(new ServerPlayerId(1)));
    }

    [Fact]
    public void StepAdvancesOneAuthoritativeTick()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        simulation.Step();

        Assert.Equal(1UL, simulation.CurrentTick);
        Assert.False(simulation.IsDisposed);
        Assert.True(simulation.StaticColliderCount > 0);
    }

    [Fact]
    public void StepDoesNotUpdateLastProcessedInputSequence()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddPlayer();

        simulation.Step();

        Assert.True(simulation.TryGetPlayer(player.PlayerId, out AuthoritativePlayerState? steppedPlayer));
        Assert.NotNull(steppedPlayer);
        Assert.Null(steppedPlayer.LastProcessedInputSequence);
    }

    [Fact]
    public async Task RunFiniteTickCountExitsAfterExactTickCount()
    {
        var options = new ServerLaunchOptions(7777, ContentCatalog.DefaultMapId, RunTicks: 5);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        ServerSimulationRunResult result = await ServerSimulationLoop.RunAsync(simulation, options);

        Assert.Equal(5UL, result.TicksRun);
        Assert.Equal(5UL, simulation.CurrentTick);
    }

    private static void AssertFinite(AuthoritativePlayerState player)
    {
        Assert.True(float.IsFinite(player.Character.Position.X));
        Assert.True(float.IsFinite(player.Character.Position.Y));
        Assert.True(float.IsFinite(player.Character.Position.Z));
        Assert.True(float.IsFinite(player.Character.Velocity.X));
        Assert.True(float.IsFinite(player.Character.Velocity.Y));
        Assert.True(float.IsFinite(player.Character.Velocity.Z));
        Assert.True(float.IsFinite(player.Look.YawRadians));
        Assert.True(float.IsFinite(player.Look.PitchRadians));
    }
}
