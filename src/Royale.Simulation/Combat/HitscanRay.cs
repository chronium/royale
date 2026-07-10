using System.Numerics;
using Royale.Simulation.Movement;

namespace Royale.Simulation.Combat;

public readonly record struct HitscanRay
{
    public HitscanRay(Vector3 origin, Vector3 direction, float length)
    {
        if (!IsFinite(origin))
            throw new ArgumentOutOfRangeException(nameof(origin), "Hitscan origin must be finite.");

        if (!IsFinite(direction) || direction.LengthSquared() <= float.Epsilon)
            throw new ArgumentOutOfRangeException(nameof(direction), "Hitscan direction must be finite and non-zero.");

        if (!float.IsFinite(length) || length <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(length), "Hitscan length must be finite and positive.");

        Origin = origin;
        Direction = Vector3.Normalize(direction);
        Length = length;
    }

    public Vector3 Origin { get; }

    public Vector3 Direction { get; }

    public float Length { get; }

    public Vector3 Translation => Direction * Length;

    public static HitscanRay FromPlayerLook(
        Vector3 feetPosition,
        PlayerLookState lookState,
        PlayerViewSettings viewSettings,
        float length,
        KinematicCharacterStance stance = KinematicCharacterStance.Standing)
    {
        ArgumentNullException.ThrowIfNull(viewSettings);

        if (!IsFinite(feetPosition))
            throw new ArgumentOutOfRangeException(nameof(feetPosition), "Player feet position must be finite.");

        if (!float.IsFinite(lookState.YawRadians) || !float.IsFinite(lookState.PitchRadians))
            throw new ArgumentOutOfRangeException(nameof(lookState), "Player look state must be finite.");

        float cosPitch = MathF.Cos(lookState.PitchRadians);
        Vector3 direction = new(
            cosPitch * MathF.Sin(lookState.YawRadians),
            MathF.Sin(lookState.PitchRadians),
            -cosPitch * MathF.Cos(lookState.YawRadians));

        return new HitscanRay(
            feetPosition + new Vector3(0.0f, viewSettings.GetEyeHeight(stance), 0.0f),
            direction,
            length);
    }

    internal static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
