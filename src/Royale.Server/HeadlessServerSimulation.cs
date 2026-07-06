using Royale.Content;
using Royale.Simulation.World;

namespace Royale.Server;

public sealed class HeadlessServerSimulation : IDisposable
{
    private readonly MapStaticCollisionWorld collisionWorld;
    private bool disposed;

    private HeadlessServerSimulation(GameMap map, MapStaticCollisionWorld collisionWorld)
    {
        MapId = map.Id;
        this.collisionWorld = collisionWorld;
    }

    public ulong CurrentTick { get; private set; }

    public string MapId { get; }

    public int StaticColliderCount => collisionWorld.ColliderCount;

    public bool IsDisposed => disposed;

    public static HeadlessServerSimulation Create(string mapId)
    {
        GameMap map = MapCatalog.LoadById(mapId);
        MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        try
        {
            return new HeadlessServerSimulation(map, collisionWorld);
        }
        catch
        {
            collisionWorld.Dispose();
            throw;
        }
    }

    public void Step()
    {
        ThrowIfDisposed();

        collisionWorld.Step(SimulationSettings.FixedDeltaSeconds, SimulationSettings.PhysicsSubStepCount);
        CurrentTick++;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        collisionWorld.Dispose();
        disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
