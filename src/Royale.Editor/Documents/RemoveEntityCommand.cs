using Royale.Content.Maps;

namespace Royale.Editor.Documents;

public sealed class RemoveEntityCommand : IEditorDocumentCommand
{
    private readonly Guid editorId;
    private readonly List<RemovedEntity> removed = [];

    public RemoveEntityCommand(Guid editorId)
    {
        if (editorId == Guid.Empty)
            throw new ArgumentException("An editor identity is required.", nameof(editorId));
        this.editorId = editorId;
    }

    public string Description => "Delete entity";
    public int IncidentLinkCount { get; private set; }

    public void Apply(EditorMapDocument document)
    {
        if (removed.Count == 0)
            Capture(document);
        foreach (RemovedEntity item in removed.OrderByDescending(item => item.Kind).ThenByDescending(item => item.Index))
            document.RemoveEntity(item.EditorId);
    }

    public void Revert(EditorMapDocument document)
    {
        foreach (RemovedEntity item in removed.OrderBy(item => item.Kind).ThenBy(item => item.Index))
            document.InsertEntity(item.Kind, item.Index, item.Definition, item.EditorId);
    }

    private void Capture(EditorMapDocument document)
    {
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        removed.Add(new RemovedEntity(identity.EditorId, identity.Kind, identity.Index, document.GetDefinition(identity.EditorId)));
        if (identity.Kind != EditorEntityKind.NavigationWaypoint)
            return;

        string waypointId = ((MapNavigationWaypoint)document.GetDefinition(editorId)).Id;
        foreach (EditorEntityIdentity linkIdentity in document.Identities.Where(value => value.Kind == EditorEntityKind.NavigationLink))
        {
            var link = (MapNavigationLink)document.GetDefinition(linkIdentity.EditorId);
            if (string.Equals(link.From, waypointId, StringComparison.Ordinal) || string.Equals(link.To, waypointId, StringComparison.Ordinal))
                removed.Add(new RemovedEntity(linkIdentity.EditorId, linkIdentity.Kind, linkIdentity.Index, link));
        }
        IncidentLinkCount = removed.Count - 1;
    }

    private sealed record RemovedEntity(Guid EditorId, EditorEntityKind Kind, int Index, object Definition);
}
