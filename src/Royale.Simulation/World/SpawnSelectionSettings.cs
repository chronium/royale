namespace Royale.Simulation.World;

public sealed record SpawnSelectionSettings
{
    public static SpawnSelectionSettings Default { get; } = new();

    public float PlayerRadius { get; init; } = 0.35f;

    public float PlayerHeight { get; init; } = 1.8f;

    public float GroundClearance { get; init; } = 0.05f;
}
