namespace Royale.Editor.Documents;

public sealed class ReplaceEntityCommand : IEditorDocumentCommand
{
    private readonly Guid editorId;
    private readonly object before;
    private readonly object after;

    public ReplaceEntityCommand(Guid editorId, object before, object after)
    {
        this.editorId = editorId;
        this.before = before ?? throw new ArgumentNullException(nameof(before));
        this.after = after ?? throw new ArgumentNullException(nameof(after));
    }

    public string Description => "Edit entity";
    public void Apply(EditorMapDocument document) => Set(document, after);
    public void Revert(EditorMapDocument document) => Set(document, before);

    private void Set(EditorMapDocument document, object value)
    {
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        EditorMapEditing.ValidateDefinition(document, identity.Kind, value, editorId);
        document.ReplaceDefinition(editorId, value);
    }
}
