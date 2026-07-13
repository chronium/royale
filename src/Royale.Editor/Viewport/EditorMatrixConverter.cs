using System.Numerics;

namespace Royale.Editor.Viewport;

public static class EditorMatrixConverter
{
    public static Matrix4x4 ToImGuizmo(Matrix4x4 numericsMatrix) => Matrix4x4.Transpose(numericsMatrix);

    public static Matrix4x4 FromImGuizmo(Matrix4x4 imGuizmoMatrix) => Matrix4x4.Transpose(imGuizmoMatrix);
}
