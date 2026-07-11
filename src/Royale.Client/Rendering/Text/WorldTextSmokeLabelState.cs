using System.Numerics;
using BlurgText;
using Royale.Rendering.Text;

namespace Royale.Client.Rendering.Text;

public sealed class WorldTextSmokeLabelState
{
    private WorldTextSmokeLabelState(IReadOnlyList<WorldTextBillboard> labels)
    {
        Labels = labels;
    }

    public IReadOnlyList<WorldTextBillboard> Labels { get; }

    public static WorldTextSmokeLabelState CreateDefault(Vector3 trainingDummyFeetPosition, float trainingDummyHeight)
    {
        Vector3 dummyLabelPosition = trainingDummyFeetPosition + new Vector3(0.0f, trainingDummyHeight + 0.35f, 0.0f);

        WorldTextBillboard cameraFacing = WorldTextBillboard.CameraFacing(
            "Training Dummy",
            dummyLabelPosition,
            0.28f,
            new Vector2(0.5f, 1.0f),
            BlurgColor.White,
            new BlurgColor(0, 0, 0, 180),
            new Vector2(2.0f, 2.0f));

        WorldTextBillboard fixedFacing = WorldTextBillboard.FixedFacing(
            "Fixed Label",
            new Vector3(-3.8f, 1.2f, -8.2f),
            0.20f,
            new Vector2(0.5f, 0.5f),
            new WorldTextBasis(Vector3.UnitX, Vector3.UnitY),
            new BlurgColor(160, 220, 255, 255),
            new BlurgColor(0, 0, 0, 180),
            new Vector2(2.0f, 2.0f));

        return new WorldTextSmokeLabelState([cameraFacing, fixedFacing]);
    }
}
