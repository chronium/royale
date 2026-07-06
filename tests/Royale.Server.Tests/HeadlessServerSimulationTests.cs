using Royale.Content;
using Royale.Server;

namespace Royale.Server.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class HeadlessServerSimulationTests
{
    [Fact]
    public void CreateLoadsDefaultGrayboxStaticCollisionWorld()
    {
        GameMap map = MapCatalog.LoadDefault();

        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Equal(ContentCatalog.DefaultMapId, simulation.MapId);
        Assert.Equal(map.StaticBoxes.Count, simulation.StaticColliderCount);
        Assert.Equal(0UL, simulation.CurrentTick);
        Assert.False(simulation.IsDisposed);
    }

    [Fact]
    public void StepAdvancesOneAuthoritativeTick()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        simulation.Step();

        Assert.Equal(1UL, simulation.CurrentTick);
        Assert.False(simulation.IsDisposed);
        Assert.True(simulation.StaticColliderCount > 0);
    }

    [Fact]
    public async Task RunFiniteTickCountExitsAfterExactTickCount()
    {
        var options = new ServerLaunchOptions(7777, ContentCatalog.DefaultMapId, RunTicks: 5);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        ServerSimulationRunResult result = await ServerSimulationLoop.RunAsync(simulation, options);

        Assert.Equal(5UL, result.TicksRun);
        Assert.Equal(5UL, simulation.CurrentTick);
    }
}
