using Royale.Simulation;

namespace Royale.Simulation.Tests;

public sealed class SimulationSettingsTests
{
    [Fact]
    public void UsesFixedSixtyHertzTickRate()
    {
        Assert.Equal(60, SimulationSettings.TickRateHz);
    }
}
