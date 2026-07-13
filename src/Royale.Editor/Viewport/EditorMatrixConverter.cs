using System.Numerics;

namespace Royale.Editor.Viewport;

public static class EditorMatrixConverter
{
    // System.Numerics uses row-vector semantics and row-major storage. ImGuizmo's matrix_t
    // also reads basis vectors and translation as four contiguous rows, so its expected
    // native memory layout is already identical despite the APIs describing conventions
    // differently. Transposing here moves translation out of ImGuizmo's fourth row.
    public static Matrix4x4 ToImGuizmo(Matrix4x4 numericsMatrix) => numericsMatrix;

    public static Matrix4x4 FromImGuizmo(Matrix4x4 imGuizmoMatrix) => imGuizmoMatrix;
}
