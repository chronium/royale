using Royale.Box3D.Bindings;

namespace Royale.Box3D;

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

        foreach (Box3DShape shape in shapes)
            shape.InvalidateFromBody();

        if (world.IsValid && Box3DBindingSurface.b3Body_IsValid(Id))
            Box3DBindingSurface.b3DestroyBody(Id);

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
