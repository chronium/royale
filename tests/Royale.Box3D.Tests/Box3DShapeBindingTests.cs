using Royale.Box3D.Bindings;

namespace Royale.Box3D.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class Box3DShapeBindingTests
{
    [Fact]
    public void DefaultShapeDefinitionUsesPinnedBox3DDefaults()
    {
        B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();

        Assert.Equal(1000.0f, shapeDef.Density);
        Assert.Equal(1.0f, shapeDef.ExplosionScale);
        Assert.False(shapeDef.IsSensor);
        Assert.False(shapeDef.EnableSensorEvents);
        Assert.True(shapeDef.InvokeContactCreation);
        Assert.True(shapeDef.UpdateBodyMass);
        Assert.Equal(ulong.MaxValue, shapeDef.Filter.CategoryBits);
        Assert.Equal(ulong.MaxValue, shapeDef.Filter.MaskBits);
        Assert.Equal(0, shapeDef.Filter.GroupIndex);
        Assert.NotEqual(0, shapeDef.InternalValue);
    }

    [Fact]
    public void StaticBoxHullShapeReportsHullTypeOwnerBodyAndWorld()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());

            B3ShapeId shapeId = CreateBoxShape(bodyId, Box3DBindingSurface.b3DefaultShapeDef());

            Assert.True(Box3DBindingSurface.b3Shape_IsValid(shapeId));
            Assert.Equal(B3ShapeType.HullShape, Box3DBindingSurface.b3Shape_GetType(shapeId));
            Assert.False(Box3DBindingSurface.b3Shape_IsSensor(shapeId));
            AssertBodyId(bodyId, Box3DBindingSurface.b3Shape_GetBody(shapeId));
            AssertWorldId(worldId, Box3DBindingSurface.b3Shape_GetWorld(shapeId));
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void CapsuleShapeReportsCapsuleTypeOwnerBodyAndWorld()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());
            B3Capsule capsule = new()
            {
                Center1 = new B3Vec3 { X = 0.0f, Y = -0.5f, Z = 0.0f },
                Center2 = new B3Vec3 { X = 0.0f, Y = 0.5f, Z = 0.0f },
                Radius = 0.25f,
            };
            B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();

            B3ShapeId shapeId = Box3DBindingSurface.b3CreateCapsuleShape(bodyId, in shapeDef, in capsule);

            Assert.True(Box3DBindingSurface.b3Shape_IsValid(shapeId));
            Assert.Equal(B3ShapeType.CapsuleShape, Box3DBindingSurface.b3Shape_GetType(shapeId));
            AssertBodyId(bodyId, Box3DBindingSurface.b3Shape_GetBody(shapeId));
            AssertWorldId(worldId, Box3DBindingSurface.b3Shape_GetWorld(shapeId));
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void ShapeFilterRoundTripsFromCreationAndMutation()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());
            B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
            shapeDef.Filter = new B3Filter
            {
                CategoryBits = 0x0002,
                MaskBits = 0x0004,
                GroupIndex = -7,
            };
            B3ShapeId shapeId = CreateBoxShape(bodyId, shapeDef);

            AssertFilter(shapeDef.Filter, Box3DBindingSurface.b3Shape_GetFilter(shapeId));

            B3Filter updatedFilter = new()
            {
                CategoryBits = 0x0010,
                MaskBits = 0x0020,
                GroupIndex = 11,
            };
            Box3DBindingSurface.b3Shape_SetFilter(shapeId, updatedFilter, invokeContacts: false);

            AssertFilter(updatedFilter, Box3DBindingSurface.b3Shape_GetFilter(shapeId));
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void DestroyShapeInvalidatesShapeWhileBodyAndWorldRemainValid()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3BodyId bodyId = CreateBody(worldId, Box3DBindingSurface.b3DefaultBodyDef());
            B3ShapeId shapeId = CreateBoxShape(bodyId, Box3DBindingSurface.b3DefaultShapeDef());

            Box3DBindingSurface.b3DestroyShape(shapeId, updateBodyMass: true);

            Assert.False(Box3DBindingSurface.b3Shape_IsValid(shapeId));
            Assert.True(Box3DBindingSurface.b3Body_IsValid(bodyId));
            Assert.True(Box3DBindingSurface.b3World_IsValid(worldId));
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

    private static B3ShapeId CreateBoxShape(B3BodyId bodyId, B3ShapeDef shapeDef)
    {
        B3BoxHull box = Box3DBindingSurface.b3MakeBoxHull(1.0f, 2.0f, 3.0f);
        B3ShapeId shapeId = Box3DBindingSurface.b3CreateHullShape(bodyId, in shapeDef, in box.Base);
        Assert.True(Box3DBindingSurface.b3Shape_IsValid(shapeId));
        return shapeId;
    }

    private static void DestroyWorldIfValid(B3WorldId worldId)
    {
        if (Box3DBindingSurface.b3World_IsValid(worldId))
        {
            Box3DBindingSurface.b3DestroyWorld(worldId);
        }
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

    private static void AssertFilter(B3Filter expected, B3Filter actual)
    {
        Assert.Equal(expected.CategoryBits, actual.CategoryBits);
        Assert.Equal(expected.MaskBits, actual.MaskBits);
        Assert.Equal(expected.GroupIndex, actual.GroupIndex);
    }
}
