namespace Royale.Simulation;

public readonly record struct PlayerLookSettings(
    float MinPitchRadians,
    float MaxPitchRadians,
    float MouseSensitivityRadiansPerPixel)
{
    public static readonly PlayerLookSettings Default = new(
        MinPitchRadians: DegreesToRadians(-89.0f),
        MaxPitchRadians: DegreesToRadians(89.0f),
        MouseSensitivityRadiansPerPixel: 0.0025f);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
