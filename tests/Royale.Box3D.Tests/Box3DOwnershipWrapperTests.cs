using Royale.Box3D.Bindings;

namespace Royale.Box3D.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class Box3DOwnershipWrapperTests
{
    [Fact]
    public void WorldCreateStepDisposeAndIdempotentDisposePreservesNativeLifecycle()
    {
        int initialWorldCount = Box3DBindingSurface.b3GetWorldCount();
        Box3DWorld world = CreateZeroGravityWorld();
        B3WorldId worldId = world.Id;

        Assert.False(world.IsDisposed);
        Assert.True(world.IsValid);
        Assert.True(Box3DBindingSurface.b3World_IsValid(worldId));

        world.Step(1.0f / 60.0f, 4);
        world.Dispose();
        world.Dispose();

        Assert.True(world.IsDisposed);
        Assert.False(world.IsValid);
        Assert.False(Box3DBindingSurface.b3World_IsValid(worldId));
        Assert.Equal(initialWorldCount, Box3DBindingSurface.b3GetWorldCount());
    }

    [Fact]
    public void BodyTransformTypeAndLinearVelocityRoundTripThroughWrapper()
    {
        using Box3DWorld world = CreateZeroGravityWorld();
        B3BodyDef bodyDef = Box3DBindingSurface.b3DefaultBodyDef();
        bodyDef.Type = B3BodyType.DynamicBody;
        Box3DBody body = world.CreateBody(in bodyDef);
        B3Pos position = new() { X = 2.5f, Y = 3.25f, Z = -4.5f };
        B3Quat rotation = new()
        {
            V = new B3Vec3 { X = 0.0f, Y = 0.0f, Z = 0.70710677f },
            S = 0.70710677f,
        };
        B3Vec3 velocity = new() { X = 1.25f, Y = -2.5f, Z = 3.75f };

        Assert.Equal(B3BodyType.DynamicBody, body.Type);
        body.Type = B3BodyType.KinematicBody;
        body.SetTransform(position, rotation);
        body.LinearVelocity = velocity;

        Assert.Equal(B3BodyType.KinematicBody, body.Type);
        AssertPosition(position, body.Position);
        AssertQuaternion(rotation, body.Rotation);
        AssertPosition(position, body.Transform.P);
        AssertQuaternion(rotation, body.Transform.Q);
        AssertVector(velocity, body.LinearVelocity);
    }

    [Fact]
    public void HullAndCapsuleShapesAreCreatedThroughBodyWrapper()
    {
        using Box3DWorld world = CreateZeroGravityWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
        B3BoxHull box = Box3DBindingSurface.b3MakeBoxHull(1.0f, 2.0f, 3.0f);
        B3Capsule capsule = new()
        {
            Center1 = new B3Vec3 { X = 0.0f, Y = -0.5f, Z = 0.0f },
            Center2 = new B3Vec3 { X = 0.0f, Y = 0.5f, Z = 0.0f },
            Radius = 0.25f,
        };

        Box3DShape hull = body.CreateHullShape(in shapeDef, in box.Base);
        Box3DShape capsuleShape = body.CreateCapsuleShape(in shapeDef, in capsule);

        Assert.True(hull.IsValid);
        Assert.Equal(B3ShapeType.HullShape, hull.Type);
        Assert.False(hull.IsSensor);
        AssertBodyId(body.Id, hull.BodyId);
        AssertWorldId(world.Id, hull.WorldId);
        Assert.True(capsuleShape.IsValid);
        Assert.Equal(B3ShapeType.CapsuleShape, capsuleShape.Type);
        AssertBodyId(body.Id, capsuleShape.BodyId);
        AssertWorldId(world.Id, capsuleShape.WorldId);
    }

    [Fact]
    public void ShapeFilterRoundTripsThroughWrapper()
    {
        using Box3DWorld world = CreateZeroGravityWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
        shapeDef.Filter = new B3Filter
        {
            CategoryBits = 0x0002,
            MaskBits = 0x0004,
            GroupIndex = -7,
        };
        Box3DShape shape = CreateBoxShape(body, shapeDef);

        AssertFilter(shapeDef.Filter, shape.Filter);

        B3Filter updatedFilter = new()
        {
            CategoryBits = 0x0010,
            MaskBits = 0x0020,
            GroupIndex = 11,
        };
        shape.SetFilter(updatedFilter, invokeContacts: false);

        AssertFilter(updatedFilter, shape.Filter);
    }

    [Fact]
    public void DisposingShapeKeepsBodyAndWorldValid()
    {
        using Box3DWorld world = CreateZeroGravityWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DShape shape = CreateBoxShape(body, Box3DBindingSurface.b3DefaultShapeDef());
        B3ShapeId shapeId = shape.Id;

        shape.Dispose();
        shape.Dispose();

        Assert.True(shape.IsDisposed);
        Assert.False(shape.IsValid);
        Assert.False(Box3DBindingSurface.b3Shape_IsValid(shapeId));
        Assert.True(body.IsValid);
        Assert.True(world.IsValid);
    }

    [Fact]
    public void DisposingBodyInvalidatesAttachedShapeWrappers()
    {
        using Box3DWorld world = CreateZeroGravityWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DShape shape = CreateBoxShape(body, Box3DBindingSurface.b3DefaultShapeDef());
        B3BodyId bodyId = body.Id;
        B3ShapeId shapeId = shape.Id;

        body.Dispose();
        body.Dispose();

        Assert.True(body.IsDisposed);
        Assert.False(body.IsValid);
        Assert.True(shape.IsDisposed);
        Assert.False(shape.IsValid);
        Assert.False(Box3DBindingSurface.b3Body_IsValid(bodyId));
        Assert.False(Box3DBindingSurface.b3Shape_IsValid(shapeId));
        Assert.True(world.IsValid);
    }

    [Fact]
    public void DisposingWorldInvalidatesBodyAndShapeWrappers()
    {
        Box3DWorld world = CreateZeroGravityWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DShape shape = CreateBoxShape(body, Box3DBindingSurface.b3DefaultShapeDef());
        B3WorldId worldId = world.Id;
        B3BodyId bodyId = body.Id;
        B3ShapeId shapeId = shape.Id;

        world.Dispose();
        world.Dispose();

        Assert.True(world.IsDisposed);
        Assert.True(body.IsDisposed);
        Assert.True(shape.IsDisposed);
        Assert.False(world.IsValid);
        Assert.False(body.IsValid);
        Assert.False(shape.IsValid);
        Assert.False(Box3DBindingSurface.b3World_IsValid(worldId));
        Assert.False(Box3DBindingSurface.b3Body_IsValid(bodyId));
        Assert.False(Box3DBindingSurface.b3Shape_IsValid(shapeId));
    }

    [Fact]
    public void LowLevelNativeIdsRemainUsableBeforeDisposal()
    {
        using Box3DWorld world = CreateZeroGravityWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DShape shape = CreateBoxShape(body, Box3DBindingSurface.b3DefaultShapeDef());

        Assert.True(Box3DBindingSurface.b3World_IsValid(world.Id));
        Assert.True(Box3DBindingSurface.b3Body_IsValid(body.Id));
        Assert.True(Box3DBindingSurface.b3Shape_IsValid(shape.Id));
        Assert.Equal(B3BodyType.StaticBody, Box3DBindingSurface.b3Body_GetType(body.Id));
        Assert.Equal(B3ShapeType.HullShape, Box3DBindingSurface.b3Shape_GetType(shape.Id));
    }

    [Fact]
    public void OperationsAfterDisposalThrowObjectDisposedException()
    {
        Box3DWorld world = CreateZeroGravityWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DShape shape = CreateBoxShape(body, Box3DBindingSurface.b3DefaultShapeDef());

        shape.Dispose();
        Assert.Throws<ObjectDisposedException>(() => shape.Type);
        Assert.Throws<ObjectDisposedException>(() => shape.SetFilter(Box3DBindingSurface.b3DefaultShapeDef().Filter, invokeContacts: false));

        body.Dispose();
        Assert.Throws<ObjectDisposedException>(() => body.Type);
        Assert.Throws<ObjectDisposedException>(() => body.Type = B3BodyType.DynamicBody);
        Assert.Throws<ObjectDisposedException>(() => body.Position);
        Assert.Throws<ObjectDisposedException>(() => body.Rotation);
        Assert.Throws<ObjectDisposedException>(() => body.Transform);
        Assert.Throws<ObjectDisposedException>(() => body.LinearVelocity);
        Assert.Throws<ObjectDisposedException>(() => body.LinearVelocity = new B3Vec3());
        Assert.Throws<ObjectDisposedException>(() => body.CreateHullShape(Box3DBindingSurface.b3DefaultShapeDef(), Box3DBindingSurface.b3MakeCubeHull(0.5f).Base));

        world.Dispose();
        Assert.Throws<ObjectDisposedException>(() => world.Step(1.0f / 60.0f, 4));
        Assert.Throws<ObjectDisposedException>(() => world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef()));
    }

    private static Box3DWorld CreateZeroGravityWorld()
    {
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();
        worldDef.Gravity = new B3Vec3 { X = 0.0f, Y = 0.0f, Z = 0.0f };
        return Box3DWorld.Create(in worldDef);
    }

    private static Box3DShape CreateBoxShape(Box3DBody body, B3ShapeDef shapeDef)
    {
        B3BoxHull box = Box3DBindingSurface.b3MakeBoxHull(1.0f, 2.0f, 3.0f);
        return body.CreateHullShape(in shapeDef, in box.Base);
    }

    private static void AssertWorldId(B3WorldId expected, B3WorldId actual)
    {
        Assert.Equal(expected.Index1, actual.Index1);
        Assert.Equal(expected.Generation, actual.Generation);
    }

    private static void AssertBodyId(B3BodyId expected, B3BodyId actual)
    {
        Assert.Equal(expected.Index1, actual.Index1);
        Assert.Equal(expected.World0, actual.World0);
        Assert.Equal(expected.Generation, actual.Generation);
    }

    private static void AssertPosition(B3Pos expected, B3Pos actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }

    private static void AssertQuaternion(B3Quat expected, B3Quat actual)
    {
        AssertVector(expected.V, actual.V);
        Assert.InRange(actual.S, expected.S - 0.0001f, expected.S + 0.0001f);
    }

    private static void AssertVector(B3Vec3 expected, B3Vec3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }

    private static void AssertFilter(B3Filter expected, B3Filter actual)
    {
        Assert.Equal(expected.CategoryBits, actual.CategoryBits);
        Assert.Equal(expected.MaskBits, actual.MaskBits);
        Assert.Equal(expected.GroupIndex, actual.GroupIndex);
    }
}
