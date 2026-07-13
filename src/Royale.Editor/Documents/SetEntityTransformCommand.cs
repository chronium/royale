namespace Royale.Editor.Documents;

public sealed class SetEntityTransformCommand : IEditorDocumentCommand
{
    private readonly Guid editorId;
    private readonly EditorEntityTransform before;
    private readonly EditorEntityTransform after;

    public SetEntityTransformCommand(Guid editorId, EditorEntityTransform before, EditorEntityTransform after)
    {
        if (editorId == Guid.Empty)
            throw new ArgumentException("An editor entity identity is required.", nameof(editorId));
        this.editorId = editorId;
        this.before = before;
        this.after = after;
    }

    public string Description => "Transform entity";

    public void Apply(EditorMapDocument document) => Set(document, after);

    public void Revert(EditorMapDocument document) => Set(document, before);

    private void Set(EditorMapDocument document, EditorEntityTransform value)
    {
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        EditorEntityTransforms.Set(document, identity, value);
    }
}
