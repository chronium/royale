using Royale.Content.Maps;

namespace Royale.Editor.Documents;

public sealed class RenameWaypointCommand : IEditorDocumentCommand
{
    private readonly Guid editorId;
    private readonly string before;
    private readonly string after;

    public RenameWaypointCommand(Guid editorId, string before, string after)
    {
        this.editorId = editorId;
        this.before = before;
        this.after = after;
    }

    public string Description => "Rename navigation waypoint";
    public void Apply(EditorMapDocument document) => Rename(document, before, after);
    public void Revert(EditorMapDocument document) => Rename(document, after, before);

    private void Rename(EditorMapDocument document, string oldId, string newId)
    {
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        var waypoint = (MapNavigationWaypoint)document.GetDefinition(editorId);
        var replacement = waypoint with { Id = newId };
        EditorMapEditing.ValidateDefinition(document, identity.Kind, replacement, editorId);
        document.ReplaceDefinition(editorId, replacement);
        foreach (EditorEntityIdentity linkIdentity in document.Identities.Where(value => value.Kind == EditorEntityKind.NavigationLink).ToArray())
        {
            var link = (MapNavigationLink)document.GetDefinition(linkIdentity.EditorId);
            if (string.Equals(link.From, oldId, StringComparison.Ordinal) || string.Equals(link.To, oldId, StringComparison.Ordinal))
            {
                document.ReplaceDefinition(linkIdentity.EditorId, link with
                {
                    From = string.Equals(link.From, oldId, StringComparison.Ordinal) ? newId : link.From,
                    To = string.Equals(link.To, oldId, StringComparison.Ordinal) ? newId : link.To,
                });
            }
        }
    }
}
