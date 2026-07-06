using Royale.Box3D.Bindings;

namespace Royale.Box3D;

public sealed class Box3DShape : IDisposable
{
    private readonly Box3DBody body;
    private bool disposed;

    internal Box3DShape(Box3DBody body, B3ShapeId id)
    {
        this.body = body;
        Id = id;
    }

    public B3ShapeId Id { get; }

    public bool IsDisposed => disposed;

    public bool IsValid => !disposed && body.IsValid && Box3DBindingSurface.b3Shape_IsValid(Id);

    public B3ShapeType Type
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Shape_GetType(Id);
        }
    }

    public B3BodyId BodyId
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Shape_GetBody(Id);
        }
    }

    public B3WorldId WorldId
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Shape_GetWorld(Id);
        }
    }

    public bool IsSensor
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Shape_IsSensor(Id);
        }
    }

    public B3Filter Filter
    {
        get
        {
            ThrowIfDisposed();
            return Box3DBindingSurface.b3Shape_GetFilter(Id);
        }
        set
        {
            SetFilter(value, invokeContacts: true);
        }
    }

    public void SetFilter(B3Filter filter, bool invokeContacts)
    {
        ThrowIfDisposed();
        Box3DBindingSurface.b3Shape_SetFilter(Id, filter, invokeContacts);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        if (body.IsValid && Box3DBindingSurface.b3Shape_IsValid(Id))
            Box3DBindingSurface.b3DestroyShape(Id, updateBodyMass: true);

        disposed = true;
    }

    internal void InvalidateFromBody()
    {
        disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
