using Royale.Box3D.Bindings.Interop;
using Royale.Box3D.Geometry;
using Royale.Box3D.Worlds;

namespace Royale.Box3D.Bodies;

public sealed class Box3DBody : IDisposable
{
    private readonly Box3DWorld world;
    private readonly List<Box3DShape> shapes = [];
    private bool disposed;

    internal Box3DBody(Box3DWorld world, B3BodyId id)
    {
        this.world = world;
        Id = id;
    }

    public B3BodyId Id { get; }

    public bool IsDisposed => disposed;

    public bool IsValid => !disposed && world.IsValid && Box3DBindingSurface.b3Body_IsValid(Id);

    public B3BodyType Type
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Body_GetType(Id);
        }
        set
        {
            ThrowIfDisposed();
            Box3DBindingSurface.b3Body_SetType(Id, value);
        }
    }

    public B3Pos Position
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Body_GetPosition(Id);
        }
    }

    public B3Quat Rotation
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Body_GetRotation(Id);
        }
    }

    public B3WorldTransform Transform
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Body_GetTransform(Id);
        }
        set
        {
            ThrowIfDisposed();
            Box3DBindingSurface.b3Body_SetTransform(Id, value.P, value.Q);
        }
    }

    public B3Vec3 LinearVelocity
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Body_GetLinearVelocity(Id);
        }
        set
        {
            ThrowIfDisposed();
            Box3DBindingSurface.b3Body_SetLinearVelocity(Id, value);
        }
    }

    public void SetTransform(B3Pos position, B3Quat rotation)
    {
        ThrowIfDisposed();
        Box3DBindingSurface.b3Body_SetTransform(Id, position, rotation);
    }

    public Box3DShape CreateHullShape(in B3ShapeDef shapeDef, in B3HullData hull)
    {
        ThrowIfDisposed();

        B3ShapeId shapeId = Box3DBindingSurface.b3CreateHullShape(Id, in shapeDef, in hull);
        if (!Box3DBindingSurface.b3Shape_IsValid(shapeId))
            throw new InvalidOperationException("Box3D did not create a valid hull shape.");

        var shape = new Box3DShape(this, shapeId);
        shapes.Add(shape);
        return shape;
    }

    public unsafe Box3DShape CreateHullShape(in B3ShapeDef shapeDef, Box3DHull hull)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(hull);

        B3HullData* nativeHull = hull.GetNativeData();
        B3ShapeId shapeId = Box3DBindingSurface.b3CreateHullShape(Id, in shapeDef, in *nativeHull);
        if (!Box3DBindingSurface.b3Shape_IsValid(shapeId))
            throw new InvalidOperationException("Box3D did not create a valid hull shape.");

        var shape = new Box3DShape(this, shapeId);
        shapes.Add(shape);
        return shape;
    }

    public unsafe Box3DShape CreateMeshShape(
        in B3ShapeDef shapeDef,
        Box3DMesh mesh,
        B3Vec3 scale)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(mesh);
        if (Type != B3BodyType.StaticBody)
            throw new InvalidOperationException("Box3D mesh shapes may only be attached to static bodies.");
        if (!float.IsFinite(scale.X) || !float.IsFinite(scale.Y) || !float.IsFinite(scale.Z) ||
            scale.X == 0.0f || scale.Y == 0.0f || scale.Z == 0.0f)
        {
            throw new ArgumentException("Mesh scale components must be finite and non-zero.", nameof(scale));
        }

        Box3DMesh.Box3DMeshReference? meshReference = mesh.AcquireShapeReference();
        try
        {
            B3ShapeId shapeId = Box3DBindingSurface.b3CreateMeshShape(
                Id,
                in shapeDef,
                meshReference.NativeData,
                scale);
            if (!Box3DBindingSurface.b3Shape_IsValid(shapeId))
                throw new InvalidOperationException("Box3D did not create a valid mesh shape.");

            var shape = new Box3DShape(this, shapeId, meshReference);
            meshReference = null;
            shapes.Add(shape);
            return shape;
        }
        finally
        {
            meshReference?.Dispose();
        }
    }

    public Box3DShape CreateCapsuleShape(in B3ShapeDef shapeDef, in B3Capsule capsule)
    {
        ThrowIfDisposed();

        B3ShapeId shapeId = Box3DBindingSurface.b3CreateCapsuleShape(Id, in shapeDef, in capsule);
        if (!Box3DBindingSurface.b3Shape_IsValid(shapeId))
            throw new InvalidOperationException("Box3D did not create a valid capsule shape.");

        var shape = new Box3DShape(this, shapeId);
        shapes.Add(shape);
        return shape;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        if (world.IsValid && Box3DBindingSurface.b3Body_IsValid(Id))
            Box3DBindingSurface.b3DestroyBody(Id);

        foreach (Box3DShape shape in shapes)
            shape.InvalidateFromBody();

        disposed = true;
    }

    internal void InvalidateFromWorld()
    {
        if (disposed)
            return;

        foreach (Box3DShape shape in shapes)
            shape.InvalidateFromBody();

        disposed = true;
    }

    internal void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
