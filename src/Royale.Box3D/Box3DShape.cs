using Royale.Box3D.Bindings;

namespace Royale.Box3D;

public sealed class Box3DShape : IDisposable
{
    private readonly Box3DBody body;
    private IDisposable? geometryLease;
    private bool disposed;

    internal Box3DShape(Box3DBody body, B3ShapeId id, IDisposable? geometryLease = null)
    {
        this.body = body;
        Id = id;
        this.geometryLease = geometryLease;
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

        try
        {
            if (body.IsValid && Box3DBindingSurface.b3Shape_IsValid(Id))
                Box3DBindingSurface.b3DestroyShape(Id, updateBodyMass: true);
        }
        finally
        {
            disposed = true;
            ReleaseGeometryLease();
        }
    }

    internal void InvalidateFromBody()
    {
        if (disposed)
            return;

        disposed = true;
        ReleaseGeometryLease();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private void ReleaseGeometryLease() =>
        Interlocked.Exchange(ref geometryLease, null)?.Dispose();
}
