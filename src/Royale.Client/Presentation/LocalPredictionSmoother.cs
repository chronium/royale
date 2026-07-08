using System.Numerics;
using Royale.Protocol;

namespace Royale.Client.Presentation;

public sealed class LocalPredictionSmoother
{
    private const float CorrectionThreshold = 0.001f;
    private const float MaxSmoothedCorrectionDistance = 1.5f;
    private const float CorrectionHalfLifeSeconds = 0.045f;
    private const float OffsetEpsilon = 0.0001f;

    private Vector3 visualOffset;
    private Vector3 lastDisplayedPosition;
    private ulong lastReconciliationCount;
    private bool hasDisplayedPlayer;

    public PlayerSnapshotState Update(
        PlayerSnapshotState predictedPlayer,
        ulong reconciliationCount,
        float correctionDistance,
        double deltaSeconds)
    {
        if (!predictedPlayer.Alive)
        {
            ResetTo(predictedPlayer.Position, reconciliationCount);
            return predictedPlayer;
        }

        if (!hasDisplayedPlayer)
        {
            ResetTo(predictedPlayer.Position, reconciliationCount);
            return predictedPlayer;
        }

        if (reconciliationCount != lastReconciliationCount)
        {
            lastReconciliationCount = reconciliationCount;

            visualOffset = correctionDistance is > CorrectionThreshold and <= MaxSmoothedCorrectionDistance
                ? lastDisplayedPosition - predictedPlayer.Position
                : Vector3.Zero;
        }

        Vector3 displayedPosition = predictedPlayer.Position + visualOffset;
        lastDisplayedPosition = displayedPosition;
        DecayOffset(deltaSeconds);

        return predictedPlayer with { Position = displayedPosition };
    }

    public void Reset()
    {
        visualOffset = Vector3.Zero;
        lastDisplayedPosition = Vector3.Zero;
        lastReconciliationCount = 0;
        hasDisplayedPlayer = false;
    }

    private void ResetTo(Vector3 position, ulong reconciliationCount)
    {
        visualOffset = Vector3.Zero;
        lastDisplayedPosition = position;
        lastReconciliationCount = reconciliationCount;
        hasDisplayedPlayer = true;
    }

    private void DecayOffset(double deltaSeconds)
    {
        if (visualOffset.LengthSquared() <= OffsetEpsilon * OffsetEpsilon)
        {
            visualOffset = Vector3.Zero;
            return;
        }

        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
            return;

        float retain = MathF.Pow(
            0.5f,
            (float)deltaSeconds / CorrectionHalfLifeSeconds);
        visualOffset *= retain;
    }
}
