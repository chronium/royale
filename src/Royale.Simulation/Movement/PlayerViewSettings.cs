namespace Royale.Simulation.Movement;

public sealed record PlayerViewSettings
{
    public const float DefaultEyeHeight = 1.62f;

    public static readonly PlayerViewSettings Default = new(DefaultEyeHeight);

    public PlayerViewSettings(float eyeHeight)
    {
        if (!float.IsFinite(eyeHeight) || eyeHeight <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(eyeHeight), "Eye height must be finite and positive.");

        EyeHeight = eyeHeight;
    }

    public float EyeHeight { get; }
}
