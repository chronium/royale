namespace Royale.Editor.Documents;

public interface IEditorDocumentCommand
{
    string Description { get; }
    void Apply(EditorMapDocument document);
    void Revert(EditorMapDocument document);
}
