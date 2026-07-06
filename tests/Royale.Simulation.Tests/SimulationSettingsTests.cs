using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Simulation.Tests;

public sealed class SimulationSettingsTests
{
    [Fact]
    public void UsesFixedSixtyHertzTickRate()
    {
        Assert.Equal(60, SimulationSettings.TickRateHz);
    }
}
