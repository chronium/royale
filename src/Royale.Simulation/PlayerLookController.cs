using System.Numerics;

namespace Royale.Simulation;

public static class PlayerLookController
{
    public static PlayerLookState ApplyMouseDelta(
        PlayerLookState state,
        Vector2 mouseDelta,
        PlayerLookSettings? settings = null)
    {
        PlayerLookSettings resolvedSettings = settings ?? PlayerLookSettings.Default;

        if (!IsFinite(state) || !IsFinite(mouseDelta) || !IsFinite(resolvedSettings))
            return IsFinite(state)
                ? ClampPitch(state, resolvedSettings)
                : new PlayerLookState(0.0f, 0.0f);

        float minPitch = MathF.Min(resolvedSettings.MinPitchRadians, resolvedSettings.MaxPitchRadians);
        float maxPitch = MathF.Max(resolvedSettings.MinPitchRadians, resolvedSettings.MaxPitchRadians);

        return new PlayerLookState(
            state.YawRadians + mouseDelta.X * resolvedSettings.MouseSensitivityRadiansPerPixel,
            Math.Clamp(
                state.PitchRadians - mouseDelta.Y * resolvedSettings.MouseSensitivityRadiansPerPixel,
                minPitch,
                maxPitch));
    }

    private static PlayerLookState ClampPitch(PlayerLookState state, PlayerLookSettings settings)
    {
        if (!float.IsFinite(settings.MinPitchRadians) || !float.IsFinite(settings.MaxPitchRadians))
            return state;

        float minPitch = MathF.Min(settings.MinPitchRadians, settings.MaxPitchRadians);
        float maxPitch = MathF.Max(settings.MinPitchRadians, settings.MaxPitchRadians);
        return state with { PitchRadians = Math.Clamp(state.PitchRadians, minPitch, maxPitch) };
    }

    private static bool IsFinite(PlayerLookState state) =>
        float.IsFinite(state.YawRadians) && float.IsFinite(state.PitchRadians);

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsFinite(PlayerLookSettings settings) =>
        float.IsFinite(settings.MinPitchRadians) &&
        float.IsFinite(settings.MaxPitchRadians) &&
        float.IsFinite(settings.MouseSensitivityRadiansPerPixel);
}
