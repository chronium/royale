using Royale.Box3D.Bindings.Interop;

using Royale.Box3D.Tests.Infrastructure;

using Royale.Box3D.Bodies;
using Royale.Box3D.Geometry;
using Royale.Box3D.Runtime;
using Royale.Box3D.Worlds;

namespace Royale.Box3D.Tests.Bindings;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class Box3DBodyBindingTests
{
    [Fact]
    public void CreatedBodyReportsDefaultStaticType()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());

            Assert.True(Box3DBindingSurface.b3Body_IsValid(bodyId));
            Assert.Equal(B3BodyType.StaticBody, Box3DBindingSurface.b3Body_GetType(bodyId));
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void BodyTypeRoundTrips()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());

            Box3DBindingSurface.b3Body_SetType(bodyId, B3BodyType.DynamicBody);
            Assert.Equal(B3BodyType.DynamicBody, Box3DBindingSurface.b3Body_GetType(bodyId));

            Box3DBindingSurface.b3Body_SetType(bodyId, B3BodyType.KinematicBody);
            Assert.Equal(B3BodyType.KinematicBody, Box3DBindingSurface.b3Body_GetType(bodyId));
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void SetTransformUpdatesPositionRotationAndTransform()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());
            B3Pos position = new() { X = 2.5f, Y = 3.25f, Z = -4.5f };
            B3Quat rotation = new()
            {
                V = new B3Vec3 { X = 0.0f, Y = 0.0f, Z = 0.70710677f },
                S = 0.70710677f,
            };

            Box3DBindingSurface.b3Body_SetTransform(bodyId, position, rotation);

            B3Pos updatedPosition = Box3DBindingSurface.b3Body_GetPosition(bodyId);
            B3Quat updatedRotation = Box3DBindingSurface.b3Body_GetRotation(bodyId);
            B3WorldTransform updatedTransform = Box3DBindingSurface.b3Body_GetTransform(bodyId);

            AssertPosition(position, updatedPosition);
            AssertQuaternion(rotation, updatedRotation);
            AssertPosition(position, updatedTransform.P);
            AssertQuaternion(rotation, updatedTransform.Q);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void LinearVelocityRoundTrips()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyDef bodyDef = Box3DBindingSurface.b3DefaultBodyDef();
            bodyDef.Type = B3BodyType.DynamicBody;
            B3BodyId bodyId = CreateBody(worldId, bodyDef);
            B3Vec3 velocity = new() { X = 1.25f, Y = -2.5f, Z = 3.75f };

            Box3DBindingSurface.b3Body_SetLinearVelocity(bodyId, velocity);

            AssertVector(velocity, Box3DBindingSurface.b3Body_GetLinearVelocity(bodyId));
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void DynamicBodyWithLinearVelocityMovesAfterWorldStep()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyDef bodyDef = Box3DBindingSurface.b3DefaultBodyDef();
            bodyDef.Type = B3BodyType.DynamicBody;
            B3BodyId bodyId = CreateBody(worldId, bodyDef);
            AttachUnitCube(bodyId);

            B3Pos initialPosition = Box3DBindingSurface.b3Body_GetPosition(bodyId);
            Box3DBindingSurface.b3Body_SetLinearVelocity(bodyId, new B3Vec3 { X = 3.0f, Y = 0.0f, Z = 0.0f });

            Box3DBindingSurface.b3World_Step(worldId, 0.5f, 4);

            B3Pos steppedPosition = Box3DBindingSurface.b3Body_GetPosition(bodyId);
            Assert.True(steppedPosition.X > initialPosition.X + 0.1f);
            Assert.InRange(steppedPosition.Y, -0.001f, 0.001f);
            Assert.InRange(steppedPosition.Z, -0.001f, 0.001f);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void DestroyBodyInvalidatesBodyWithoutDestroyingWorld()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());

            Box3DBindingSurface.b3DestroyBody(bodyId);

            Assert.False(Box3DBindingSurface.b3Body_IsValid(bodyId));
            Assert.True(Box3DBindingSurface.b3World_IsValid(worldId));

            B3BodyId replacementBodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());
            Assert.True(Box3DBindingSurface.b3Body_IsValid(replacementBodyId));
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    private static B3WorldId CreateWorld()
    {
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();
        worldDef.Gravity = new B3Vec3 { X = 0.0f, Y = 0.0f, Z = 0.0f };
        return Box3DBindingSurface.b3CreateWorld(in worldDef);
    }

    private static B3BodyId CreateBody(B3WorldId worldId, B3BodyDef bodyDef)
    {
        B3BodyId bodyId = Box3DBindingSurface.b3CreateBody(worldId, in bodyDef);
        Assert.True(Box3DBindingSurface.b3Body_IsValid(bodyId));
        return bodyId;
    }

    private static void AttachUnitCube(B3BodyId bodyId)
    {
        B3BoxHull box = Box3DBindingSurface.b3MakeCubeHull(0.5f);
        B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
        shapeDef.Density = 1.0f;

        B3ShapeId shapeId = Box3DBindingSurface.b3CreateHullShape(bodyId, in shapeDef, in box.Base);
        Assert.NotEqual(0, shapeId.Index1);
    }

    private static void DestroyWorldIfValid(B3WorldId worldId)
    {
        if (Box3DBindingSurface.b3World_IsValid(worldId))
        {
            Box3DBindingSurface.b3DestroyWorld(worldId);
        }
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
}
