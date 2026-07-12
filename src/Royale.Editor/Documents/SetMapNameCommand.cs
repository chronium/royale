namespace Royale.Editor.Documents;

public sealed class SetMapNameCommand : IEditorDocumentCommand
{
    private readonly string before;
    private readonly string after;
    public SetMapNameCommand(string before, string after)
    {
        if (string.IsNullOrWhiteSpace(after)) throw new ArgumentException("Map name must be non-empty.", nameof(after));
        this.before = before; this.after = after.Trim();
    }
    public string Description => "Rename map";
    public void Apply(EditorMapDocument document) => document.Map = document.Map with { Name = after };
    public void Revert(EditorMapDocument document) => document.Map = document.Map with { Name = before };
}
