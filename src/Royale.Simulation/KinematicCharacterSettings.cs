namespace Royale.Simulation;

public sealed record KinematicCharacterSettings
{
    public float Radius { get; init; } = 0.35f;

    public float Height { get; init; } = 1.8f;

    public float WalkSpeed { get; init; } = 4.5f;

    public float JumpApexHeight { get; init; } = 1.1f;

    public float Gravity { get; init; } = 20.0f;

    public float MaxStepHeight { get; init; } = 0.35f;

    public float SlopeLimitDegrees { get; init; } = 45.0f;

    public float GroundProbeDistance { get; init; } = 0.08f;

    public float SkinWidth { get; init; } = 0.01f;

    public int MaxSlideIterations { get; init; } = 4;

    public int PenetrationRecoveryIterations { get; init; } = 4;

    public float PenetrationRecoveryDistance { get; init; } = 0.02f;
}
