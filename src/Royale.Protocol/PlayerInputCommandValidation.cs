namespace Royale.Protocol;

public static class PlayerInputCommandValidation
{
    public const float MaxMoveMagnitude = 1.0f;
    public const float MoveMagnitudeTolerance = 0.0001f;
    public static readonly float MinPitchRadians = DegreesToRadians(-89.0f);
    public static readonly float MaxPitchRadians = DegreesToRadians(89.0f);

    private const InputButtons DefinedButtons =
        InputButtons.Jump |
        InputButtons.Fire |
        InputButtons.Reload |
        InputButtons.Interact |
        InputButtons.Crouch |
        InputButtons.Sprint;

    public static bool IsValid(PlayerInputCommand command) =>
        IsFinite(command) &&
        IsMoveMagnitudeValid(command.Move) &&
        IsPitchValid(command.PitchRadians) &&
        AreButtonsDefined(command.Buttons);

    public static bool AreButtonsDefined(InputButtons buttons) =>
        (((ushort)buttons) & ~(ushort)DefinedButtons) == 0;

    private static bool IsFinite(PlayerInputCommand command) =>
        float.IsFinite(command.Move.X) &&
        float.IsFinite(command.Move.Y) &&
        float.IsFinite(command.YawRadians) &&
        float.IsFinite(command.PitchRadians);

    private static bool IsMoveMagnitudeValid(System.Numerics.Vector2 move)
    {
        float maxMagnitude = MaxMoveMagnitude + MoveMagnitudeTolerance;
        return move.LengthSquared() <= maxMagnitude * maxMagnitude;
    }

    private static bool IsPitchValid(float pitchRadians) =>
        pitchRadians >= MinPitchRadians && pitchRadians <= MaxPitchRadians;

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
