using Royale.Editor.Documents;

namespace Royale.Editor.Workspace;

public sealed class EditorSelectionState
{
    public Guid? SelectedEditorId { get; private set; }

    public bool HasSelection => SelectedEditorId.HasValue;

    public void Select(EditorMapDocument document, Guid editorId)
    {
        document.GetIdentity(editorId);
        SelectedEditorId = editorId;
    }

    public EditorEntityIdentity? Resolve(EditorMapDocument document) =>
        SelectedEditorId is Guid editorId && document.TryGetIdentity(editorId, out EditorEntityIdentity identity)
            ? identity
            : null;

    public void Clear() => SelectedEditorId = null;
}
