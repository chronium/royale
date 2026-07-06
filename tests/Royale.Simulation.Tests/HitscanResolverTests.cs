using System.Numerics;
using Royale.Content;
using Royale.Simulation;

namespace Royale.Simulation.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class HitscanResolverTests
{
    [Fact]
    public void PlayerLookRayUsesEyePositionAndHitsNearestStaticCollider()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWallMap());
        HitscanRay ray = HitscanRay.FromPlayerLook(
            Vector3.Zero,
            new PlayerLookState(0.0f, 0.0f),
            PlayerViewSettings.Default,
            WeaponCatalog.DefaultRifle.RangeMeters);

        HitscanHit hit = HitscanResolver.Resolve(collisionWorld, ray);

        Assert.True(hit.IsStatic);
        Assert.Equal("near-wall", hit.StaticColliderId);
        AssertVector(new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, -4.75f), hit.Point, 0.02f);
        Assert.InRange(hit.Distance, 4.70f, 4.80f);
        Assert.Equal(PlayerViewSettings.DefaultEyeHeight, ray.Origin.Y, precision: 4);
    }

    [Fact]
    public void RayWithNoStaticObstructionReturnsNoHit()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateEmptyMap());
        HitscanRay ray = HitscanRay.FromPlayerLook(
            Vector3.Zero,
            new PlayerLookState(0.0f, 0.0f),
            PlayerViewSettings.Default,
            WeaponCatalog.DefaultRifle.RangeMeters);

        HitscanHit hit = HitscanResolver.Resolve(collisionWorld, ray);

        Assert.False(hit.Hit);
        Assert.Equal(HitscanHit.None, hit);
    }

    [Fact]
    public void CapsuleTargetInFrontOfShooterIsHit()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateEmptyMap());
        HitscanRay ray = HitscanRay.FromPlayerLook(
            Vector3.Zero,
            new PlayerLookState(0.0f, 0.0f),
            PlayerViewSettings.Default,
            WeaponCatalog.DefaultRifle.RangeMeters);

        HitscanHit hit = HitscanResolver.Resolve(collisionWorld, ray, [DefaultTarget("target-a", new Vector3(0.0f, 0.0f, -10.0f))]);

        Assert.True(hit.IsTarget);
        Assert.Equal("target-a", hit.TargetId);
        Assert.InRange(hit.Distance, 9.65f, 9.75f);
        AssertVector(new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, -9.70f), hit.Point, 0.06f);
    }

    [Fact]
    public void StaticWallBetweenShooterAndCapsuleTargetWins()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateWallMap());
        HitscanRay ray = HitscanRay.FromPlayerLook(
            Vector3.Zero,
            new PlayerLookState(0.0f, 0.0f),
            PlayerViewSettings.Default,
            WeaponCatalog.DefaultRifle.RangeMeters);

        HitscanHit hit = HitscanResolver.Resolve(collisionWorld, ray, [DefaultTarget("target-a", new Vector3(0.0f, 0.0f, -10.0f))]);

        Assert.True(hit.IsStatic);
        Assert.Equal("near-wall", hit.StaticColliderId);
        Assert.InRange(hit.Distance, 4.70f, 4.80f);
    }

    [Fact]
    public void NearestCapsuleTargetWins()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateEmptyMap());
        HitscanRay ray = HitscanRay.FromPlayerLook(
            Vector3.Zero,
            new PlayerLookState(0.0f, 0.0f),
            PlayerViewSettings.Default,
            WeaponCatalog.DefaultRifle.RangeMeters);

        HitscanHit hit = HitscanResolver.Resolve(collisionWorld, ray,
        [
            DefaultTarget("far-target", new Vector3(0.0f, 0.0f, -12.0f)),
            DefaultTarget("near-target", new Vector3(0.0f, 0.0f, -6.0f)),
        ]);

        Assert.True(hit.IsTarget);
        Assert.Equal("near-target", hit.TargetId);
        Assert.InRange(hit.Distance, 5.65f, 5.75f);
    }

    [Fact]
    public void TargetsBehindOriginOutsideRangeOrOutsideCapsuleRadiusAreIgnored()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(CreateEmptyMap());
        HitscanRay ray = HitscanRay.FromPlayerLook(
            Vector3.Zero,
            new PlayerLookState(0.0f, 0.0f),
            PlayerViewSettings.Default,
            length: 4.0f);

        HitscanHit hit = HitscanResolver.Resolve(collisionWorld, ray,
        [
            DefaultTarget("behind", new Vector3(0.0f, 0.0f, 4.0f)),
            DefaultTarget("outside-range", new Vector3(0.0f, 0.0f, -6.0f)),
            DefaultTarget("lateral-miss", new Vector3(1.0f, 0.0f, -2.0f)),
        ]);

        Assert.False(hit.Hit);
    }

    [Fact]
    public void InvalidRayArgumentsThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HitscanRay(
            new Vector3(float.NaN, 0.0f, 0.0f),
            -Vector3.UnitZ,
            10.0f));

        Assert.Throws<ArgumentOutOfRangeException>(() => new HitscanRay(
            Vector3.Zero,
            Vector3.Zero,
            10.0f));

        Assert.Throws<ArgumentOutOfRangeException>(() => new HitscanRay(
            Vector3.Zero,
            -Vector3.UnitZ,
            float.PositiveInfinity));
    }

    private static HitscanTarget DefaultTarget(string id, Vector3 feetPosition) =>
        HitscanTarget.FromCharacter(
            id,
            new KinematicCharacterState(feetPosition, Vector3.Zero, IsGrounded: true),
            new KinematicCharacterSettings());

    private static GameMap CreateEmptyMap() => new()
    {
        Id = "empty-map",
        Name = "Empty Map",
    };

    private static GameMap CreateWallMap() => new()
    {
        Id = "wall-map",
        Name = "Wall Map",
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "near-wall",
                Position = new MapVector3(0.0f, 1.5f, -5.0f),
                Size = new MapVector3(8.0f, 3.0f, 0.5f),
            },
            new StaticBoxDefinition
            {
                Id = "far-wall",
                Position = new MapVector3(0.0f, 1.5f, -12.0f),
                Size = new MapVector3(8.0f, 3.0f, 0.5f),
            },
        ],
    };

    private static void AssertVector(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
        Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
        Assert.InRange(actual.Z, expected.Z - tolerance, expected.Z + tolerance);
    }
}
