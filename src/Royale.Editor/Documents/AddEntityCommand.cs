namespace Royale.Editor.Documents;

public sealed class AddEntityCommand : IEditorDocumentCommand
{
    private readonly EditorEntityKind kind;
    private readonly int index;
    private readonly object definition;
    private readonly Guid editorId;

    public AddEntityCommand(EditorEntityKind kind, int index, object definition, Guid? editorId = null)
    {
        this.kind = kind;
        this.index = index;
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.editorId = editorId ?? Guid.NewGuid();
    }

    public Guid EditorId => editorId;
    public string Description => "Add entity";

    public void Apply(EditorMapDocument document)
    {
        EditorMapEditing.ValidateDefinition(document, kind, definition);
        document.InsertEntity(kind, index, definition, editorId);
    }

    public void Revert(EditorMapDocument document) => document.RemoveEntity(editorId);
}
