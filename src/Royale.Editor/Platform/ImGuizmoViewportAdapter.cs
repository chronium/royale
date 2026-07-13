using System.Numerics;
using Evergine.Bindings.Imgui;
using Evergine.Bindings.Imguizmo;
using Royale.Editor.Documents;
using Royale.Editor.Viewport;
using Royale.Editor.Viewport.FaceSnap;
using Royale.Rendering.Cameras;

namespace Royale.Editor.Platform;

internal readonly record struct ImGuizmoFrameResult(
    EditorEntityTransform Transform,
    bool Changed,
    bool IsUsing,
    bool IsHovered,
    EditorTranslationConstraint TranslationConstraint);

internal static unsafe class ImGuizmoViewportAdapter
{
    private static EditorTranslationConstraint activeTranslationConstraint;

    public static void BeginFrame() => ImguizmoNative.ImGuizmo_BeginFrame();

    public static ImGuizmoFrameResult Manipulate(
        RenderCamera camera,
        uint viewportWidth,
        uint viewportHeight,
        Evergine.Mathematics.Vector2 screenPosition,
        Evergine.Mathematics.Vector2 screenSize,
        EditorEntityTransform transform,
        EditorTransformSettings settings,
        EditorTransformCapabilities capabilities)
    {
        EditorTransformOperation operation = ResolveOperation(settings.Operation, capabilities);
        Matrix4x4 view = EditorMatrixConverter.ToImGuizmo(camera.CreateViewMatrix());
        Matrix4x4 projection = EditorMatrixConverter.ToImGuizmo(camera.CreateProjectionMatrix(viewportWidth, viewportHeight));
        Matrix4x4 matrix = EditorMatrixConverter.ToImGuizmo(transform.CreateMatrix());
        Vector3 snap = settings.GetSnapVector(operation);
        bool wasUsing = ImguizmoNative.ImGuizmo_IsUsing();
        EditorTranslationConstraint hoveredConstraint = operation == EditorTransformOperation.Translate && !wasUsing
            ? ResolveHoveredTranslationConstraint()
            : EditorTranslationConstraint.None;

        ImguizmoNative.ImGuizmo_SetDrawlist(ImguiNative.igGetWindowDrawList());
        ImguizmoNative.ImGuizmo_SetOrthographic(false);
        ImguizmoNative.ImGuizmo_SetRect(screenPosition.X, screenPosition.Y, screenSize.X, screenSize.Y);
        bool changed;
        Matrix4x4* viewPointer = &view;
        Matrix4x4* projectionPointer = &projection;
        Matrix4x4* matrixPointer = &matrix;
        float* snapPointer = settings.SnappingEnabled ? (float*)&snap : null;
        changed = ImguizmoNative.ImGuizmo_Manipulate(
            (float*)viewPointer,
            (float*)projectionPointer,
            ToNative(operation),
            settings.Orientation == EditorTransformOrientation.Local ? MODE.LOCAL : MODE.WORLD,
            (float*)matrixPointer,
            null,
            snapPointer,
            null,
            null);

        EditorEntityTransform result = changed
            ? EditorEntityTransform.FromMatrix(EditorMatrixConverter.FromImGuizmo(matrix))
            : transform;
        result = PreserveUnsupportedComponents(transform, result, capabilities);
        bool isUsing = ImguizmoNative.ImGuizmo_IsUsing();
        if (!wasUsing && isUsing)
            activeTranslationConstraint = hoveredConstraint;
        EditorTranslationConstraint constraint = isUsing || wasUsing
            ? activeTranslationConstraint
            : EditorTranslationConstraint.None;
        if (!isUsing)
            activeTranslationConstraint = EditorTranslationConstraint.None;

        return new ImGuizmoFrameResult(
            result,
            changed,
            isUsing,
            ImguizmoNative.ImGuizmo_IsOver_Nil(),
            constraint);
    }

    public static EditorTransformOperation ResolveOperation(
        EditorTransformOperation requested,
        EditorTransformCapabilities capabilities)
    {
        EditorTransformCapabilities required = requested switch
        {
            EditorTransformOperation.Translate => EditorTransformCapabilities.Translate,
            EditorTransformOperation.Rotate => EditorTransformCapabilities.Rotate,
            EditorTransformOperation.Scale => EditorTransformCapabilities.Scale,
            _ => EditorTransformCapabilities.None,
        };
        return capabilities.HasFlag(required) ? requested : EditorTransformOperation.Translate;
    }

    private static EditorEntityTransform PreserveUnsupportedComponents(
        EditorEntityTransform before,
        EditorEntityTransform after,
        EditorTransformCapabilities capabilities) => new(
            capabilities.HasFlag(EditorTransformCapabilities.Translate) ? after.Position : before.Position,
            capabilities.HasFlag(EditorTransformCapabilities.Rotate) ? after.RotationDegrees : before.RotationDegrees,
            capabilities.HasFlag(EditorTransformCapabilities.Scale) ? after.ScaleOrSize : before.ScaleOrSize);

    private static OPERATION ToNative(EditorTransformOperation operation) => operation switch
    {
        EditorTransformOperation.Rotate => OPERATION.ROTATE,
        EditorTransformOperation.Scale => OPERATION.SCALE,
        _ => OPERATION.TRANSLATE,
    };

    private static EditorTranslationConstraint ResolveHoveredTranslationConstraint() =>
        EditorTranslationConstraintResolver.Resolve(
            ImguizmoNative.ImGuizmo_IsOver_OPERATION(OPERATION.TRANSLATE_X),
            ImguizmoNative.ImGuizmo_IsOver_OPERATION(OPERATION.TRANSLATE_Y),
            ImguizmoNative.ImGuizmo_IsOver_OPERATION(OPERATION.TRANSLATE_Z),
            ImguizmoNative.ImGuizmo_IsOver_OPERATION(OPERATION.TRANSLATE_X | OPERATION.TRANSLATE_Y),
            ImguizmoNative.ImGuizmo_IsOver_OPERATION(OPERATION.TRANSLATE_Y | OPERATION.TRANSLATE_Z),
            ImguizmoNative.ImGuizmo_IsOver_OPERATION(OPERATION.TRANSLATE_X | OPERATION.TRANSLATE_Z));
}
