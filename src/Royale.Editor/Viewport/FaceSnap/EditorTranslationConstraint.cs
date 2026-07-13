using System.Numerics;
using Royale.Editor.Documents;

namespace Royale.Editor.Viewport.FaceSnap;

[Flags]
public enum EditorTranslationConstraint
{
    None = 0,
    X = 1,
    Y = 2,
    Z = 4,
    XY = X | Y,
    YZ = Y | Z,
    XZ = X | Z,
}

public static class EditorTranslationConstraintResolver
{
    public static EditorTranslationConstraint Resolve(
        bool xHovered,
        bool yHovered,
        bool zHovered,
        bool xyHovered,
        bool yzHovered,
        bool xzHovered)
    {
        if (xHovered)
            return EditorTranslationConstraint.X;
        if (yHovered)
            return EditorTranslationConstraint.Y;
        if (zHovered)
            return EditorTranslationConstraint.Z;
        if (xyHovered)
            return EditorTranslationConstraint.XY;
        if (yzHovered)
            return EditorTranslationConstraint.YZ;
        if (xzHovered)
            return EditorTranslationConstraint.XZ;
        return EditorTranslationConstraint.None;
    }

    public static (Vector3 X, Vector3 Y, Vector3 Z) CreateBasis(
        EditorEntityTransform transform,
        EditorTransformOrientation orientation)
    {
        if (orientation == EditorTransformOrientation.World)
            return (Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

        Matrix4x4 rotation = (transform with
        {
            Position = Vector3.Zero,
            ScaleOrSize = Vector3.One,
        }).CreateMatrix();
        return (
            Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, rotation)),
            Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, rotation)),
            Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, rotation)));
    }
}
