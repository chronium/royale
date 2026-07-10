namespace Royale.Simulation.Movement;

public sealed record PlayerViewSettings
{
    public const float DefaultEyeHeight = 1.62f;
    public const float DefaultCrouchedEyeHeight = 0.95f;

    public static readonly PlayerViewSettings Default = new(DefaultEyeHeight, DefaultCrouchedEyeHeight);

    public PlayerViewSettings(float eyeHeight, float crouchedEyeHeight = DefaultCrouchedEyeHeight)
    {
        if (!float.IsFinite(eyeHeight) || eyeHeight <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(eyeHeight), "Eye height must be finite and positive.");
        if (!float.IsFinite(crouchedEyeHeight) || crouchedEyeHeight <= 0.0f || crouchedEyeHeight > eyeHeight)
            throw new ArgumentOutOfRangeException(nameof(crouchedEyeHeight), "Crouched eye height must be finite, positive, and not exceed standing eye height.");

        EyeHeight = eyeHeight;
        CrouchedEyeHeight = crouchedEyeHeight;
    }

    public float EyeHeight { get; }

    public float CrouchedEyeHeight { get; }

    public float GetEyeHeight(KinematicCharacterStance stance) => stance switch
    {
        KinematicCharacterStance.Standing => EyeHeight,
        KinematicCharacterStance.Crouched => CrouchedEyeHeight,
        _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, "Unknown character stance."),
    };
}
