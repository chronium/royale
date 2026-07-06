using Royale.Content;

namespace Royale.Simulation.World;

public static class SimulationSettings
{
    public const int TickRateHz = 60;
    public const float FixedDeltaSeconds = 1.0f / TickRateHz;
    public const int PhysicsSubStepCount = 4;

    public static string DefaultMapId => ContentCatalog.DefaultMapId;
}
