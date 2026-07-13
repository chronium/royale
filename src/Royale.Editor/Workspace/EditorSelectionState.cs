using Royale.Editor.Documents;

namespace Royale.Editor.Workspace;

public sealed class EditorSelectionState
{
    public Guid? SelectedEditorId { get; private set; }

    public bool HasSelection => SelectedEditorId.HasValue;

    public void Select(EditorMapDocument document, Guid editorId)
    {
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        if (EditorEntityTransforms.GetCapabilities(identity.Kind) == EditorTransformCapabilities.None)
            throw new InvalidOperationException("The requested editor entity is not spatially selectable.");
        SelectedEditorId = editorId;
    }

    public EditorEntityIdentity? Resolve(EditorMapDocument document) =>
        SelectedEditorId is Guid editorId && document.TryGetIdentity(editorId, out EditorEntityIdentity identity)
            ? identity
            : null;

    public void Clear() => SelectedEditorId = null;
}
