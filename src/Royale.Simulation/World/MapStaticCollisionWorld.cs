using System.Numerics;
using System.Runtime.InteropServices;
using Royale.Box3D;
using Royale.Box3D.Bindings;
using Royale.Content;
using Royale.Simulation.Debug;

namespace Royale.Simulation.World;

public sealed unsafe class MapStaticCollisionWorld : IDisposable
{
    private static readonly B3OverlapResultFcn OverlapCallback = OnOverlapResult;
    private static readonly B3PlaneResultFcn PlaneCallback = OnPlaneResult;
    private static readonly B3CreateDebugShapeFcn CreateDebugShapeCallback = OnCreateDebugShape;
    private static readonly B3DestroyDebugShapeFcn DestroyDebugShapeCallback = OnDestroyDebugShape;

    private readonly Dictionary<B3ShapeId, MapStaticCollider> collidersByShape = [];
    private readonly List<MapStaticCollider> colliders = [];
    private readonly Box3DWorld world;
    private bool disposed;

    private MapStaticCollisionWorld(Box3DWorld world)
    {
        this.world = world;
    }

    public B3WorldId WorldId => world.Id;

    public IReadOnlyList<MapStaticCollider> Colliders => colliders;

    public int ColliderCount => colliders.Count;

    public bool IsDisposed => disposed;

    public static MapStaticCollisionWorld Create(GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();
        worldDef.Gravity = ToB3Vector(Vector3.Zero);
        worldDef.CreateDebugShape = Marshal.GetFunctionPointerForDelegate(CreateDebugShapeCallback);
        worldDef.DestroyDebugShape = Marshal.GetFunctionPointerForDelegate(DestroyDebugShapeCallback);
        Box3DWorld world = Box3DWorld.Create(in worldDef);

        try
        {
            if (!world.IsValid)
                throw new InvalidOperationException("Box3D did not create a valid map collision world.");

            var collisionWorld = new MapStaticCollisionWorld(world);

            foreach (StaticBoxDefinition staticBox in map.StaticBoxes)
                collisionWorld.CreateStaticBoxCollider(staticBox);

            return collisionWorld;
        }
        catch
        {
            world.Dispose();

            throw;
        }
    }

    public B3RayResult CastRayClosest(MapVector3 origin, MapVector3 translation)
    {
        ThrowIfDisposed();

        return Box3DBindingSurface.b3World_CastRayClosest(
            WorldId,
            ToB3Position(origin),
            ToB3Vector(translation),
            Box3DBindingSurface.b3DefaultQueryFilter());
    }

    public IReadOnlyList<MapStaticCollider> OverlapAabb(MapVector3 lowerBound, MapVector3 upperBound)
    {
        ThrowIfDisposed();

        var context = new OverlapQueryContext();
        using CallbackHandle handle = new(context);

        Box3DBindingSurface.b3World_OverlapAABB(
            WorldId,
            new B3Aabb
            {
                LowerBound = ToB3Vector(lowerBound),
                UpperBound = ToB3Vector(upperBound),
            },
            Box3DBindingSurface.b3DefaultQueryFilter(),
            OverlapCallback,
            handle.Pointer);

        return context.ShapeIds
            .Select(shapeId => collidersByShape[shapeId])
            .ToArray();
    }

    public MapStaticCapsuleCast CastCapsuleMover(Vector3 feetPosition, float radius, float height, Vector3 translation)
    {
        ThrowIfDisposed();
        ValidateCapsuleDimensions(radius, height);

        float fraction = Box3DBindingSurface.b3World_CastMover(
            WorldId,
            new B3Pos(),
            CreateFeetAnchoredCapsule(feetPosition, radius, height),
            ToB3Vector(translation),
            Box3DBindingSurface.b3DefaultQueryFilter(),
            fcn: null,
            context: nint.Zero);

        return new MapStaticCapsuleCast(Math.Clamp(fraction, 0.0f, 1.0f));
    }

    public IReadOnlyList<MapStaticCollisionPlane> CollectCapsuleCollisionPlanes(Vector3 feetPosition, float radius, float height)
    {
        ThrowIfDisposed();
        ValidateCapsuleDimensions(radius, height);

        var context = new PlaneQueryContext(this);
        using CallbackHandle handle = new(context);

        Box3DBindingSurface.b3World_CollideMover(
            WorldId,
            new B3Pos(),
            CreateFeetAnchoredCapsule(feetPosition, radius, height),
            Box3DBindingSurface.b3DefaultQueryFilter(),
            PlaneCallback,
            handle.Pointer);

        return context.Planes.ToArray();
    }

    public bool TryGetCollider(B3ShapeId shapeId, out MapStaticCollider? collider)
    {
        ThrowIfDisposed();
        return collidersByShape.TryGetValue(shapeId, out collider);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        world.Dispose();

        disposed = true;
    }

    private void CreateStaticBoxCollider(StaticBoxDefinition staticBox)
    {
        B3BodyDef bodyDef = Box3DBindingSurface.b3DefaultBodyDef();
        bodyDef.Type = B3BodyType.StaticBody;
        bodyDef.Position = ToB3Position(staticBox.Position);
        bodyDef.Rotation = ToB3Quaternion(MapStaticBoxTransforms.CreateRotation(staticBox));

        Box3DBody body = world.CreateBody(in bodyDef);
        if (!body.IsValid)
            throw new InvalidOperationException($"Box3D did not create a valid static body for map box '{staticBox.Id}'.");

        B3ShapeDef shapeDef = Box3DBindingSurface.b3DefaultShapeDef();
        B3BoxHull box = Box3DBindingSurface.b3MakeBoxHull(
            staticBox.Size.X * 0.5f,
            staticBox.Size.Y * 0.5f,
            staticBox.Size.Z * 0.5f);
        Box3DShape shape = body.CreateHullShape(in shapeDef, in box.Base);
        if (!shape.IsValid)
            throw new InvalidOperationException($"Box3D did not create a valid hull shape for map box '{staticBox.Id}'.");

        var collider = new MapStaticCollider(staticBox.Id, body.Id, shape.Id);
        colliders.Add(collider);
        collidersByShape.Add(shape.Id, collider);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static bool OnOverlapResult(B3ShapeId shapeId, nint context)
    {
        var callbackContext = (OverlapQueryContext)GCHandle.FromIntPtr(context).Target!;
        callbackContext.ShapeIds.Add(shapeId);
        return true;
    }

    private static nint OnCreateDebugShape(B3DebugShape* debugShape, nint context)
    {
        if (debugShape is null)
            return nint.Zero;

        Box3DDebugShapeGeometry geometry = Box3DDebugShapeGeometry.Create(in *debugShape);
        return GCHandle.ToIntPtr(GCHandle.Alloc(geometry));
    }

    private static void OnDestroyDebugShape(nint userShape, nint context)
    {
        if (userShape == nint.Zero)
            return;

        GCHandle handle = GCHandle.FromIntPtr(userShape);
        if (handle.IsAllocated)
            handle.Free();
    }

    private static unsafe bool OnPlaneResult(B3ShapeId shapeId, B3PlaneResult* planes, int planeCount, nint context)
    {
        var callbackContext = (PlaneQueryContext)GCHandle.FromIntPtr(context).Target!;
        callbackContext.World.TryGetCollider(shapeId, out MapStaticCollider? collider);

        for (int i = 0; i < planeCount; i++)
        {
            B3PlaneResult plane = planes[i];
            callbackContext.Planes.Add(new MapStaticCollisionPlane(
                collider,
                ToVector3(plane.Plane.Normal),
                ToVector3(plane.Point),
                plane.Plane.Offset));
        }

        return true;
    }

    private static B3Capsule CreateFeetAnchoredCapsule(Vector3 feetPosition, float radius, float height)
    {
        return new B3Capsule
        {
            Center1 = ToB3Vector(feetPosition + new Vector3(0.0f, radius, 0.0f)),
            Center2 = ToB3Vector(feetPosition + new Vector3(0.0f, height - radius, 0.0f)),
            Radius = radius,
        };
    }

    private static void ValidateCapsuleDimensions(float radius, float height)
    {
        if (!float.IsFinite(radius) || radius <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Capsule radius must be finite and positive.");

        if (!float.IsFinite(height) || height < radius * 2.0f)
            throw new ArgumentOutOfRangeException(nameof(height), "Capsule height must be finite and at least twice the radius.");
    }

    private static B3Pos ToB3Position(MapVector3 vector) => new()
    {
        X = vector.X,
        Y = vector.Y,
        Z = vector.Z,
    };

    private static B3Vec3 ToB3Vector(MapVector3 vector) => new()
    {
        X = vector.X,
        Y = vector.Y,
        Z = vector.Z,
    };

    private static B3Vec3 ToB3Vector(Vector3 vector) => new()
    {
        X = vector.X,
        Y = vector.Y,
        Z = vector.Z,
    };

    private static Vector3 ToVector3(B3Vec3 vector) => new(vector.X, vector.Y, vector.Z);

    private static B3Quat ToB3Quaternion(Quaternion quaternion)
    {
        Quaternion normalized = Quaternion.Normalize(quaternion);

        return new B3Quat
        {
            V = new B3Vec3
            {
                X = normalized.X,
                Y = normalized.Y,
                Z = normalized.Z,
            },
            S = normalized.W,
        };
    }

    private sealed class OverlapQueryContext
    {
        public List<B3ShapeId> ShapeIds { get; } = [];
    }

    private sealed class PlaneQueryContext(MapStaticCollisionWorld world)
    {
        public MapStaticCollisionWorld World { get; } = world;

        public List<MapStaticCollisionPlane> Planes { get; } = [];
    }

    private sealed class CallbackHandle : IDisposable
    {
        private GCHandle handle;

        public CallbackHandle(object target)
        {
            handle = GCHandle.Alloc(target);
        }

        public nint Pointer => GCHandle.ToIntPtr(handle);

        public void Dispose()
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}
