using System.Numerics;

namespace Royale.Editor.Viewport.FaceSnap;

public enum EditorFaceSnapAxis
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ,
}

public readonly record struct EditorFaceSnapSettings(
    bool AlignmentEnabled = false,
    EditorFaceSnapAxis AlignmentAxis = EditorFaceSnapAxis.PositiveY)
{
    public Vector3 GetLocalAxis() => AlignmentAxis switch
    {
        EditorFaceSnapAxis.PositiveX => Vector3.UnitX,
        EditorFaceSnapAxis.NegativeX => -Vector3.UnitX,
        EditorFaceSnapAxis.PositiveY => Vector3.UnitY,
        EditorFaceSnapAxis.NegativeY => -Vector3.UnitY,
        EditorFaceSnapAxis.PositiveZ => Vector3.UnitZ,
        EditorFaceSnapAxis.NegativeZ => -Vector3.UnitZ,
        _ => throw new ArgumentOutOfRangeException(nameof(AlignmentAxis)),
    };
}
