namespace Royale.Simulation.Movement;

public sealed record KinematicCharacterSettings
{
    private float standingSpeed = 4.5f;

    public float Radius { get; init; } = 0.35f;

    public float StandingHeight { get; init; } = 1.8f;

    public float CrouchedHeight { get; init; } = 1.1f;

    public float StandingSpeed
    {
        get => standingSpeed;
        init => standingSpeed = value;
    }

    public float CrouchedSpeed { get; init; } = 2.5f;

    public float Height => StandingHeight;

    public float WalkSpeed
    {
        get => standingSpeed;
        init => standingSpeed = value;
    }

    public float GetHeight(KinematicCharacterStance stance) => stance switch
    {
        KinematicCharacterStance.Standing => StandingHeight,
        KinematicCharacterStance.Crouched => CrouchedHeight,
        _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, "Unknown character stance."),
    };

    public float GetSpeed(KinematicCharacterStance stance) => stance switch
    {
        KinematicCharacterStance.Standing => StandingSpeed,
        KinematicCharacterStance.Crouched => CrouchedSpeed,
        _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, "Unknown character stance."),
    };

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
