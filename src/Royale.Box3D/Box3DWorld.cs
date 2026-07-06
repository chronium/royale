using Royale.Box3D.Bindings;

namespace Royale.Box3D;

public sealed class Box3DWorld : IDisposable
{
    private readonly List<Box3DBody> bodies = [];
    private bool disposed;

    private Box3DWorld(B3WorldId id)
    {
        Id = id;
    }

    public B3WorldId Id { get; }

    public bool IsDisposed => disposed;

    public bool IsValid => !disposed && Box3DBindingSurface.b3World_IsValid(Id);

    public static Box3DWorld Create(in B3WorldDef worldDef)
    {
        B3WorldId worldId = Box3DBindingSurface.b3CreateWorld(in worldDef);
        if (!Box3DBindingSurface.b3World_IsValid(worldId))
            throw new InvalidOperationException("Box3D did not create a valid world.");

        return new Box3DWorld(worldId);
    }

    public static Box3DWorld CreateDefault()
    {
        B3WorldDef worldDef = Box3DBindingSurface.b3DefaultWorldDef();
        return Create(in worldDef);
    }

    public void Step(float timeStep, int subStepCount)
    {
        ThrowIfDisposed();
        Box3DBindingSurface.b3World_Step(Id, timeStep, subStepCount);
    }

    public Box3DBody CreateBody(in B3BodyDef bodyDef)
    {
        ThrowIfDisposed();

        B3BodyId bodyId = Box3DBindingSurface.b3CreateBody(Id, in bodyDef);
        if (!Box3DBindingSurface.b3Body_IsValid(bodyId))
            throw new InvalidOperationException("Box3D did not create a valid body.");

        var body = new Box3DBody(this, bodyId);
        bodies.Add(body);
        return body;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        foreach (Box3DBody body in bodies)
            body.InvalidateFromWorld();

        if (Box3DBindingSurface.b3World_IsValid(Id))
            Box3DBindingSurface.b3DestroyWorld(Id);

        disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
