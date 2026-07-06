using Royale.Box3D.Bindings;
using Royale.Content;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Simulation.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class MapStaticCollisionWorldTests
{
    [Fact]
    public void LoadingDefaultGrayboxAndBuildingStaticCollisionWorldSucceeds()
    {
        GameMap map = MapCatalog.LoadDefault();

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.True(Box3DBindingSurface.b3World_IsValid(collisionWorld.WorldId));
        Assert.Equal(map.StaticBoxes.Count, collisionWorld.ColliderCount);
    }

    [Fact]
    public void StaticCollisionWorldCreatesOneValidStaticColliderPerStaticBox()
    {
        GameMap map = MapCatalog.LoadDefault();

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.Equal(map.StaticBoxes.Select(staticBox => staticBox.Id), collisionWorld.Colliders.Select(collider => collider.StaticBoxId));
        foreach (MapStaticCollider collider in collisionWorld.Colliders)
        {
            Assert.True(Box3DBindingSurface.b3Body_IsValid(collider.BodyId));
            Assert.True(Box3DBindingSurface.b3Shape_IsValid(collider.ShapeId));
            Assert.Equal(B3BodyType.StaticBody, Box3DBindingSurface.b3Body_GetType(collider.BodyId));
            Assert.Equal(B3ShapeType.HullShape, Box3DBindingSurface.b3Shape_GetType(collider.ShapeId));
        }
    }

    [Fact]
    public void ShapeIdsMapBackToStaticBoxIds()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        foreach (MapStaticCollider collider in collisionWorld.Colliders)
        {
            Assert.True(collisionWorld.TryGetCollider(collider.ShapeId, out MapStaticCollider? resolved));
            Assert.NotNull(resolved);
            Assert.Equal(collider.StaticBoxId, resolved.StaticBoxId);
        }
    }

    [Fact]
    public void DownwardRaycastAboveFloorHitsGroundCollider()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        B3RayResult result = collisionWorld.CastRayClosest(
            new MapVector3(4.0f, 3.0f, 4.0f),
            new MapVector3(0.0f, -5.0f, 0.0f));

        Assert.True(result.Hit);
        Assert.True(collisionWorld.TryGetCollider(result.ShapeId, out MapStaticCollider? collider));
        Assert.Equal("ground-main", collider!.StaticBoxId);
        Assert.InRange(result.Point.Y, -0.001f, 0.001f);
    }

    [Fact]
    public void RaycastTowardWallHitsStaticMapGeometry()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        B3RayResult result = collisionWorld.CastRayClosest(
            new MapVector3(0.0f, 0.85f, 0.0f),
            new MapVector3(0.0f, 0.0f, -8.0f));

        Assert.True(result.Hit);
        Assert.True(collisionWorld.TryGetCollider(result.ShapeId, out MapStaticCollider? collider));
        Assert.Contains(collider!.StaticBoxId, new[] { "wall-center-long", "boundary-north-wall" });
    }

    [Fact]
    public void OverlapAabbAroundCoverFindsStaticShape()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        IReadOnlyList<MapStaticCollider> colliders = collisionWorld.OverlapAabb(
            new MapVector3(0.1f, 0.1f, 0.1f),
            new MapVector3(1.3f, 0.9f, 1.3f));

        Assert.Contains(colliders, collider => collider.StaticBoxId == "cover-center-block");
    }

    [Fact]
    public void RampRotationIsAppliedToStaticColliderTransform()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());
        MapStaticCollider ramp = Assert.Single(collisionWorld.Colliders, collider => collider.StaticBoxId == "ramp-platform-approach");

        B3Quat rotation = Box3DBindingSurface.b3Body_GetRotation(ramp.BodyId);

        Assert.InRange(Math.Abs(rotation.V.X), 0.15f, 0.17f);
        Assert.InRange(Math.Abs(rotation.V.Y), 0.0f, 0.001f);
        Assert.InRange(Math.Abs(rotation.V.Z), 0.0f, 0.001f);
        Assert.InRange(Math.Abs(rotation.S), 0.98f, 0.99f);
    }

    [Fact]
    public void OverlapAabbAroundRampFindsRotatedStaticShape()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        IReadOnlyList<MapStaticCollider> colliders = collisionWorld.OverlapAabb(
            new MapVector3(-6.95f, 0.65f, 2.7f),
            new MapVector3(-5.25f, 0.9f, 3.15f));

        Assert.Contains(colliders, collider => collider.StaticBoxId == "ramp-platform-approach");
    }

    [Fact]
    public void DisposingDestroysWorldAndInvalidatesOwnedNativeIds()
    {
        MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());
        B3WorldId worldId = collisionWorld.WorldId;
        B3BodyId bodyId = collisionWorld.Colliders[0].BodyId;
        B3ShapeId shapeId = collisionWorld.Colliders[0].ShapeId;

        collisionWorld.Dispose();

        Assert.True(collisionWorld.IsDisposed);
        Assert.False(Box3DBindingSurface.b3World_IsValid(worldId));
        Assert.False(Box3DBindingSurface.b3Body_IsValid(bodyId));
        Assert.False(Box3DBindingSurface.b3Shape_IsValid(shapeId));
        Assert.Throws<ObjectDisposedException>(() => collisionWorld.CastRayClosest(new MapVector3(), new MapVector3(0.0f, -1.0f, 0.0f)));
    }
}
