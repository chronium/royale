using System.Runtime.InteropServices;
using Royale.Box3D.Bindings;

namespace Royale.Box3D.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public unsafe sealed class Box3DGeneratedCollisionTests
{
    private static readonly B3DrawBoundsFcn BoundsCallback = OnDrawBounds;
    private static readonly Box3DMeshCreationSettings MeshSettings = new(
        WeldTolerance: 0.0f,
        WeldVertices: false,
        UseMedianSplit: false,
        IdentifyEdges: true);

    [Fact]
    public void LowLevelHullAndMeshCreationRemainAvailable()
    {
        B3Vec3[] hullPoints = CubePoints(1.0f);
        fixed (B3Vec3* points = hullPoints)
        {
            B3HullData* hull = Box3DBindingSurface.b3CreateHull(points, hullPoints.Length, hullPoints.Length);
            Assert.NotEqual(nint.Zero, (nint)hull);
            Assert.Equal(8, hull->VertexCount);
            Assert.True(hull->Volume > 0.0f);
            Box3DBindingSurface.b3DestroyHull(hull);
        }

        B3Vec3[] vertices = FloorVertices();
        int[] indices = FloorIndices();
        fixed (B3Vec3* vertexPointer = vertices)
        fixed (int* indexPointer = indices)
        {
            var definition = new B3MeshDef
            {
                Vertices = vertexPointer,
                Indices = indexPointer,
                VertexCount = vertices.Length,
                TriangleCount = indices.Length / 3,
                IdentifyEdges = true,
            };
            B3MeshData* mesh = Box3DBindingSurface.b3CreateMesh(in definition, null, 0);
            Assert.NotEqual(nint.Zero, (nint)mesh);
            Assert.Equal(vertices.Length, mesh->VertexCount);
            Assert.Equal(indices.Length / 3, mesh->TriangleCount);
            Assert.True(Box3DBindingSurface.b3GetHeight(mesh) >= 0);
            Box3DBindingSurface.b3DestroyMesh(mesh);
        }
    }

    [Fact]
    public void ManagedHullShapeClonesSourceAndRemainsQueryableAfterHullDisposal()
    {
        using Box3DWorld world = CreateWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DHull hull = Box3DHull.Create(CubePoints(1.0f));
        Box3DShape shape = body.CreateHullShape(Box3DBindingSurface.b3DefaultShapeDef(), hull);

        hull.Dispose();

        Assert.True(hull.IsDisposed);
        Assert.True(hull.IsNativeReleased);
        Assert.True(shape.IsValid);
        world.Step(1.0f / 60.0f, 4);
        B3RayResult hit = CastRay(world.Id, new B3Vec3 { X = -3.0f }, new B3Vec3 { X = 6.0f });
        Assert.True(hit.Hit);
        AssertShapeId(shape.Id, hit.ShapeId);
        Assert.InRange(hit.Point.X, -1.001f, -0.999f);
    }

    [Fact]
    public void MeshShapeRetainsNativeMeshUntilShapeDisposalAndSupportsQueries()
    {
        using Box3DWorld world = CreateWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DMesh mesh = Box3DMesh.Create(FloorVertices(), FloorIndices(), in MeshSettings);
        Box3DShape shape = body.CreateMeshShape(
            Box3DBindingSurface.b3DefaultShapeDef(),
            mesh,
            new B3Vec3 { X = 1.0f, Y = 1.0f, Z = 1.0f });

        mesh.Dispose();

        Assert.True(mesh.IsDisposed);
        Assert.False(mesh.IsNativeReleased);
        Assert.Equal(B3ShapeType.MeshShape, shape.Type);
        world.Step(1.0f / 60.0f, 4);
        B3RayResult hit = CastRay(
            world.Id,
            new B3Vec3 { Y = 2.0f },
            new B3Vec3 { Y = -4.0f });
        Assert.True(hit.Hit);
        AssertShapeId(shape.Id, hit.ShapeId);
        Assert.InRange(hit.Point.Y, -0.001f, 0.001f);

        shape.Dispose();
        Assert.True(mesh.IsNativeReleased);
    }

    [Fact]
    public void BodyAndWorldDisposalReleaseRetainedMeshReferences()
    {
        Box3DWorld world = CreateWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DMesh mesh = Box3DMesh.Create(FloorVertices(), FloorIndices(), in MeshSettings);
        body.CreateMeshShape(
            Box3DBindingSurface.b3DefaultShapeDef(),
            mesh,
            new B3Vec3 { X = 1.0f, Y = 1.0f, Z = 1.0f });
        mesh.Dispose();

        Assert.False(mesh.IsNativeReleased);
        world.Dispose();
        Assert.True(mesh.IsNativeReleased);
    }

    [Fact]
    public void BodyDisposalDestroysNativeShapeBeforeReleasingMesh()
    {
        using Box3DWorld world = CreateWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        Box3DMesh mesh = Box3DMesh.Create(FloorVertices(), FloorIndices(), in MeshSettings);
        body.CreateMeshShape(
            Box3DBindingSurface.b3DefaultShapeDef(),
            mesh,
            new B3Vec3 { X = 1.0f, Y = 1.0f, Z = 1.0f });
        mesh.Dispose();

        body.Dispose();

        Assert.True(mesh.IsNativeReleased);
        Assert.True(world.IsValid);
    }

    [Fact]
    public void GeneratedCollisionShapesParticipateInDebugDraw()
    {
        using Box3DWorld world = CreateWorld();
        Box3DBody body = world.CreateBody(Box3DBindingSurface.b3DefaultBodyDef());
        using Box3DHull hull = Box3DHull.Create(CubePoints(0.5f));
        using Box3DMesh mesh = Box3DMesh.Create(FloorVertices(), FloorIndices(), in MeshSettings);
        body.CreateHullShape(Box3DBindingSurface.b3DefaultShapeDef(), hull);
        body.CreateMeshShape(
            Box3DBindingSurface.b3DefaultShapeDef(),
            mesh,
            new B3Vec3 { X = 1.0f, Y = 1.0f, Z = 1.0f });
        world.Step(1.0f / 60.0f, 4);

        var context = new DrawContext();
        using CallbackHandle handle = new(context);
        B3DebugDraw draw = Box3DBindingSurface.b3DefaultDebugDraw();
        draw.DrawBoundsFcn = Marshal.GetFunctionPointerForDelegate(BoundsCallback);
        draw.DrawBounds = true;
        draw.Context = handle.Pointer;
        Box3DBindingSurface.b3World_Draw(world.Id, ref draw, Box3DBindingSurface.B3DefaultMaskBits);

        Assert.True(context.BoundsCount >= 2);
    }

    [Fact]
    public void ManagedResourcesRejectInvalidGeneratedGeometryAndUsage()
    {
        Assert.Throws<InvalidDataException>(() => Box3DHull.Create(
            [new B3Vec3(), new B3Vec3 { X = 1.0f }, new B3Vec3 { Z = 1.0f }, new B3Vec3 { X = 1.0f, Z = 1.0f }]));
        Assert.Throws<ArgumentException>(() => Box3DMesh.Create(
            FloorVertices(),
            [0, 1, 9],
            in MeshSettings));
        Assert.Throws<InvalidDataException>(() => Box3DMesh.Create(
            FloorVertices(),
            [0, 0, 1],
            in MeshSettings));

        using Box3DWorld world = CreateWorld();
        B3BodyDef dynamicDefinition = Box3DBindingSurface.b3DefaultBodyDef();
        dynamicDefinition.Type = B3BodyType.DynamicBody;
        Box3DBody dynamicBody = world.CreateBody(in dynamicDefinition);
        using Box3DMesh mesh = Box3DMesh.Create(FloorVertices(), FloorIndices(), in MeshSettings);
        Assert.Throws<InvalidOperationException>(() => dynamicBody.CreateMeshShape(
            Box3DBindingSurface.b3DefaultShapeDef(),
            mesh,
            new B3Vec3 { X = 1.0f, Y = 1.0f, Z = 1.0f }));
    }

    private static Box3DWorld CreateWorld()
    {
        B3WorldDef definition = Box3DBindingSurface.b3DefaultWorldDef();
        definition.Gravity = new B3Vec3();
        return Box3DWorld.Create(in definition);
    }

    private static B3RayResult CastRay(B3WorldId worldId, B3Vec3 origin, B3Vec3 translation) =>
        Box3DBindingSurface.b3World_CastRayClosest(
            worldId,
            new B3Pos { X = origin.X, Y = origin.Y, Z = origin.Z },
            translation,
            Box3DBindingSurface.b3DefaultQueryFilter());

    private static B3Vec3[] CubePoints(float halfWidth) =>
    [
        new() { X = -halfWidth, Y = -halfWidth, Z = -halfWidth },
        new() { X = -halfWidth, Y = -halfWidth, Z = halfWidth },
        new() { X = -halfWidth, Y = halfWidth, Z = -halfWidth },
        new() { X = -halfWidth, Y = halfWidth, Z = halfWidth },
        new() { X = halfWidth, Y = -halfWidth, Z = -halfWidth },
        new() { X = halfWidth, Y = -halfWidth, Z = halfWidth },
        new() { X = halfWidth, Y = halfWidth, Z = -halfWidth },
        new() { X = halfWidth, Y = halfWidth, Z = halfWidth },
    ];

    private static B3Vec3[] FloorVertices() =>
    [
        new() { X = -2.0f, Z = -2.0f },
        new() { X = -2.0f, Z = 2.0f },
        new() { X = 2.0f, Z = -2.0f },
        new() { X = 2.0f, Z = 2.0f },
    ];

    private static int[] FloorIndices() => [0, 1, 2, 2, 1, 3];

    private static void OnDrawBounds(B3Aabb bounds, B3HexColor color, nint context)
    {
        _ = bounds;
        _ = color;
        ((DrawContext)GCHandle.FromIntPtr(context).Target!).BoundsCount++;
    }

    private static void AssertShapeId(B3ShapeId expected, B3ShapeId actual)
    {
        Assert.Equal(expected.Index1, actual.Index1);
        Assert.Equal(expected.World0, actual.World0);
        Assert.Equal(expected.Generation, actual.Generation);
    }

    private sealed class DrawContext
    {
        public int BoundsCount { get; set; }
    }

    private sealed class CallbackHandle : IDisposable
    {
        private GCHandle handle;

        public CallbackHandle(object target) => handle = GCHandle.Alloc(target);

        public nint Pointer => GCHandle.ToIntPtr(handle);

        public void Dispose()
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}
