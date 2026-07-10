using Royale.Simulation.Movement;

namespace Royale.Client.Rendering.Cameras;

public sealed class PlayerEyeHeightSmoother
{
    public const float TransitionSeconds = 0.15f;

    private bool initialized;
    private float current;

    public float Update(float target, double deltaSeconds)
    {
        if (!float.IsFinite(target) || target <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(target));

        if (!initialized || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            initialized = true;
            current = target;
            return current;
        }

        float maximumChange = MathF.Abs(PlayerViewSettings.DefaultEyeHeight - PlayerViewSettings.DefaultCrouchedEyeHeight) *
            (float)deltaSeconds / TransitionSeconds;
        current = MathF.Abs(target - current) <= maximumChange
            ? target
            : current + MathF.CopySign(maximumChange, target - current);
        return current;
    }

    public void Reset()
    {
        initialized = false;
        current = 0.0f;
    }
}
