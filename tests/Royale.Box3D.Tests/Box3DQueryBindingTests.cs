using System.Runtime.InteropServices;
using Royale.Box3D.Bindings;

namespace Royale.Box3D.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public unsafe sealed class Box3DQueryBindingTests
{
    [Fact]
    public void DefaultQueryFilterUsesPinnedBox3DDefaults()
    {
        B3QueryFilter filter = Box3DBindingSurface.b3DefaultQueryFilter();

        Assert.Equal(ulong.MaxValue, filter.CategoryBits);
        Assert.Equal(ulong.MaxValue, filter.MaskBits);
        Assert.Equal(0UL, filter.Id);
        Assert.Equal(nint.Zero, filter.Name);
    }

    [Fact]
    public void CastRayClosestHitsStaticBox()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3ShapeId shapeId = CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);

            B3RayResult result = Box3DBindingSurface.b3World_CastRayClosest(
                worldId,
                Position(-5.0f, 0.0f, 0.0f),
                Vector(10.0f, 0.0f, 0.0f),
                Box3DBindingSurface.b3DefaultQueryFilter());

            Assert.True(result.Hit);
            AssertShapeId(shapeId, result.ShapeId);
            Assert.InRange(result.Fraction, 0.39f, 0.41f);
            Assert.InRange(result.Point.X, -1.001f, -0.999f);
            Assert.InRange(result.Point.Y, -0.001f, 0.001f);
            Assert.InRange(result.Normal.X, -1.001f, -0.999f);
            Assert.True(result.NodeVisits > 0);
            Assert.True(result.LeafVisits > 0);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void CastRayCallbackReceivesHitAndCanStopTraversal()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3ShapeId shapeId = CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);
            QueryCallbackContext context = new() { CastReturn = 0.0f };

            using CallbackHandle handle = new(context);
            B3TreeStats stats = Box3DBindingSurface.b3World_CastRay(
                worldId,
                Position(-5.0f, 0.0f, 0.0f),
                Vector(10.0f, 0.0f, 0.0f),
                Box3DBindingSurface.b3DefaultQueryFilter(),
                CastResult,
                handle.Pointer);

            Assert.Equal(1, context.HitCount);
            AssertShapeId(shapeId, context.LastShapeId);
            Assert.InRange(context.LastFraction, 0.39f, 0.41f);
            Assert.InRange(context.LastPoint.X, -1.001f, -0.999f);
            Assert.True(stats.NodeVisits > 0);
            Assert.True(stats.LeafVisits > 0);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void OverlapAabbFindsStaticBox()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3ShapeId shapeId = CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);
            QueryCallbackContext context = new();

            using CallbackHandle handle = new(context);
            B3TreeStats stats = Box3DBindingSurface.b3World_OverlapAABB(
                worldId,
                new B3Aabb
                {
                    LowerBound = Vector(-0.25f, -0.25f, -0.25f),
                    UpperBound = Vector(0.25f, 0.25f, 0.25f),
                },
                Box3DBindingSurface.b3DefaultQueryFilter(),
                OverlapResult,
                handle.Pointer);

            Assert.Equal(1, context.HitCount);
            AssertShapeId(shapeId, context.LastShapeId);
            Assert.True(stats.NodeVisits > 0);
            Assert.True(stats.LeafVisits > 0);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void OverlapShapeFindsStaticBox()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3ShapeId shapeId = CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);
            B3Vec3 point = Vector(0.0f, 0.0f, 0.0f);
            B3ShapeProxy proxy = new()
            {
                Points = (nint)(&point),
                Count = 1,
                Radius = 0.5f,
            };
            QueryCallbackContext context = new();

            using CallbackHandle handle = new(context);
            B3TreeStats stats = Box3DBindingSurface.b3World_OverlapShape(
                worldId,
                Position(0.0f, 0.0f, 0.0f),
                in proxy,
                Box3DBindingSurface.b3DefaultQueryFilter(),
                OverlapResult,
                handle.Pointer);

            Assert.Equal(1, context.HitCount);
            AssertShapeId(shapeId, context.LastShapeId);
            Assert.True(stats.NodeVisits > 0);
            Assert.True(stats.LeafVisits > 0);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void CastShapeReportsHitAgainstStaticBox()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3ShapeId shapeId = CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);
            B3Vec3 point = Vector(-5.0f, 0.0f, 0.0f);
            B3ShapeProxy proxy = new()
            {
                Points = (nint)(&point),
                Count = 1,
                Radius = 0.25f,
            };
            QueryCallbackContext context = new();

            using CallbackHandle handle = new(context);
            B3TreeStats stats = Box3DBindingSurface.b3World_CastShape(
                worldId,
                Position(0.0f, 0.0f, 0.0f),
                in proxy,
                Vector(10.0f, 0.0f, 0.0f),
                Box3DBindingSurface.b3DefaultQueryFilter(),
                CastResult,
                handle.Pointer);

            Assert.Equal(1, context.HitCount);
            AssertShapeId(shapeId, context.LastShapeId);
            Assert.InRange(context.LastFraction, 0.35f, 0.40f);
            Assert.InRange(context.LastPoint.X, -1.001f, -0.999f);
            Assert.InRange(context.LastNormal.X, -1.001f, -0.999f);
            Assert.True(stats.NodeVisits > 0);
            Assert.True(stats.LeafVisits > 0);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void CastMoverReturnsBlockedFractionAgainstStaticBox()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);
            B3Capsule mover = new()
            {
                Center1 = Vector(-5.0f, -0.3f, 0.0f),
                Center2 = Vector(-5.0f, 0.3f, 0.0f),
                Radius = 0.25f,
            };

            float fraction = Box3DBindingSurface.b3World_CastMover(
                worldId,
                Position(0.0f, 0.0f, 0.0f),
                in mover,
                Vector(10.0f, 0.0f, 0.0f),
                Box3DBindingSurface.b3DefaultQueryFilter(),
                fcn: null,
                context: nint.Zero);

            Assert.InRange(fraction, 0.35f, 0.40f);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void CollideMoverReportsCollisionPlaneNearBlockingGeometry()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3ShapeId shapeId = CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);
            B3Capsule mover = new()
            {
                Center1 = Vector(-1.1f, -0.3f, 0.0f),
                Center2 = Vector(-1.1f, 0.3f, 0.0f),
                Radius = 0.3f,
            };
            QueryCallbackContext context = new();

            using CallbackHandle handle = new(context);
            Box3DBindingSurface.b3World_CollideMover(
                worldId,
                Position(0.0f, 0.0f, 0.0f),
                in mover,
                Box3DBindingSurface.b3DefaultQueryFilter(),
                PlaneResult,
                handle.Pointer);

            Assert.Equal(1, context.HitCount);
            AssertShapeId(shapeId, context.LastShapeId);
            Assert.True(context.PlaneCount > 0);
            Assert.InRange(Math.Abs(context.FirstPlane.Plane.Normal.X), 0.5f, 1.001f);
            Assert.InRange(context.FirstPlane.Point.X, -1.001f, -0.7f);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void QueryFiltersExcludeShapesWhenBitsDoNotMatch()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
            shapeDef.Filter = new B3Filter
            {
                CategoryBits = 0x0002,
                MaskBits = 0x0002,
                GroupIndex = 0,
            };
            CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), shapeDef);
            StepWorld(worldId);
            B3QueryFilter queryFilter = Box3DBindingSurface.b3DefaultQueryFilter();
            queryFilter.CategoryBits = 0x0001;
            queryFilter.MaskBits = 0x0001;

            B3RayResult result = Box3DBindingSurface.b3World_CastRayClosest(
                worldId,
                Position(-5.0f, 0.0f, 0.0f),
                Vector(10.0f, 0.0f, 0.0f),
                queryFilter);

            Assert.False(result.Hit);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    [Fact]
    public void CastMoverFilterCanRejectBlockingShape()
    {
        B3WorldId worldId = CreateWorld();
        try
        {
            CreateStaticBox(worldId, Position(0.0f, 0.0f, 0.0f), Box3DBindingSurface.b3DefaultShapeDef());
            StepWorld(worldId);
            B3Capsule mover = new()
            {
                Center1 = Vector(-5.0f, -0.3f, 0.0f),
                Center2 = Vector(-5.0f, 0.3f, 0.0f),
                Radius = 0.25f,
            };
            QueryCallbackContext context = new() { MoverAccept = false };

            using CallbackHandle handle = new(context);
            float fraction = Box3DBindingSurface.b3World_CastMover(
                worldId,
                Position(0.0f, 0.0f, 0.0f),
                in mover,
                Vector(10.0f, 0.0f, 0.0f),
                Box3DBindingSurface.b3DefaultQueryFilter(),
                MoverFilter,
                handle.Pointer);

            Assert.Equal(1.0f, fraction);
            Assert.Equal(1, context.HitCount);
        }
        finally
        {
            DestroyWorldIfValid(worldId);
        }
    }

    private static B3WorldId CreateWorld()
    {
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();
        worldDef.Gravity = Vector(0.0f, 0.0f, 0.0f);
        return Box3DBindingSurface.b3CreateWorld(in worldDef);
    }

    private static B3ShapeId CreateStaticBox(B3WorldId worldId, B3Pos position, B3ShapeDef shapeDef)
    {
        B3BodyDef bodyDef = Box3DBindingSurface.b3DefaultBodyDef();
        bodyDef.Type = B3BodyType.StaticBody;
        bodyDef.Position = position;
        B3BodyId bodyId = Box3DBindingSurface.b3CreateBody(worldId, in bodyDef);
        Assert.True(Box3DBindingSurface.b3Body_IsValid(bodyId));

        B3BoxHull box = Box3DBindingSurface.b3MakeBoxHull(1.0f, 1.0f, 1.0f);
        B3ShapeId shapeId = Box3DBindingSurface.b3CreateHullShape(bodyId, in shapeDef, in box.Base);
        Assert.True(Box3DBindingSurface.b3Shape_IsValid(shapeId));
        return shapeId;
    }

    private static void StepWorld(B3WorldId worldId)
    {
        Box3DBindingSurface.b3World_Step(worldId, 1.0f / 60.0f, 1);
    }

    private static void DestroyWorldIfValid(B3WorldId worldId)
    {
        if (Box3DBindingSurface.b3World_IsValid(worldId))
        {
            Box3DBindingSurface.b3DestroyWorld(worldId);
        }
    }

    private static bool OverlapResult(B3ShapeId shapeId, nint context)
    {
        QueryCallbackContext callbackContext = GetContext(context);
        callbackContext.HitCount++;
        callbackContext.LastShapeId = shapeId;
        return callbackContext.Continue;
    }

    private static float CastResult(
        B3ShapeId shapeId,
        B3Pos point,
        B3Vec3 normal,
        float fraction,
        ulong userMaterialId,
        int triangleIndex,
        int childIndex,
        nint context)
    {
        QueryCallbackContext callbackContext = GetContext(context);
        callbackContext.HitCount++;
        callbackContext.LastShapeId = shapeId;
        callbackContext.LastPoint = point;
        callbackContext.LastNormal = normal;
        callbackContext.LastFraction = fraction;
        callbackContext.LastUserMaterialId = userMaterialId;
        callbackContext.LastTriangleIndex = triangleIndex;
        callbackContext.LastChildIndex = childIndex;
        return callbackContext.CastReturn ?? fraction;
    }

    private static bool MoverFilter(B3ShapeId shapeId, nint context)
    {
        QueryCallbackContext callbackContext = GetContext(context);
        callbackContext.HitCount++;
        callbackContext.LastShapeId = shapeId;
        return callbackContext.MoverAccept;
    }

    private static bool PlaneResult(B3ShapeId shapeId, B3PlaneResult* planes, int planeCount, nint context)
    {
        QueryCallbackContext callbackContext = GetContext(context);
        callbackContext.HitCount++;
        callbackContext.LastShapeId = shapeId;
        callbackContext.PlaneCount += planeCount;
        if (planeCount > 0)
        {
            callbackContext.FirstPlane = planes[0];
        }

        return callbackContext.Continue;
    }

    private static QueryCallbackContext GetContext(nint context)
    {
        return (QueryCallbackContext)GCHandle.FromIntPtr(context).Target!;
    }

    private static void AssertShapeId(B3ShapeId expected, B3ShapeId actual)
    {
        Assert.Equal(expected.Index1, actual.Index1);
        Assert.Equal(expected.World0, actual.World0);
        Assert.Equal(expected.Generation, actual.Generation);
    }

    private static B3Pos Position(float x, float y, float z)
    {
        return new B3Pos { X = x, Y = y, Z = z };
    }

    private static B3Vec3 Vector(float x, float y, float z)
    {
        return new B3Vec3 { X = x, Y = y, Z = z };
    }

    private sealed class QueryCallbackContext
    {
        public bool Continue = true;
        public bool MoverAccept = true;
        public float? CastReturn;
        public int HitCount;
        public int PlaneCount;
        public B3ShapeId LastShapeId;
        public B3Pos LastPoint;
        public B3Vec3 LastNormal;
        public float LastFraction;
        public ulong LastUserMaterialId;
        public int LastTriangleIndex;
        public int LastChildIndex;
        public B3PlaneResult FirstPlane;
    }

    private readonly struct CallbackHandle : IDisposable
    {
        private readonly GCHandle handle;

        public CallbackHandle(QueryCallbackContext context)
        {
            handle = GCHandle.Alloc(context);
            Pointer = GCHandle.ToIntPtr(handle);
        }

        public nint Pointer { get; }

        public void Dispose()
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }
}
