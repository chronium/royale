using Royale.Content.Maps;

namespace Royale.Editor.Documents;

public sealed class EditorMapDocument
{
    private readonly List<IEditorDocumentCommand> history = [];
    private int historyPosition;
    private int savedPosition;

    public EditorMapDocument(GameMap map, string? sourcePath, string? sourceFingerprint, bool requiresSaveAs)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        SourcePath = sourcePath;
        SourceFingerprint = sourceFingerprint;
        RequiresSaveAs = requiresSaveAs;
        Identities = CreateIdentities(map);
    }

    public GameMap Map { get; internal set; }
    public string? SourcePath { get; private set; }
    public string? SourceFingerprint { get; private set; }
    public bool RequiresSaveAs { get; private set; }
    public IReadOnlyList<EditorEntityIdentity> Identities { get; }
    public long Revision { get; private set; }
    public bool IsDirty => historyPosition != savedPosition;
    public bool CanUndo => historyPosition > 0;
    public bool CanRedo => historyPosition < history.Count;
    public string? UndoDescription => CanUndo ? history[historyPosition - 1].Description : null;
    public string? RedoDescription => CanRedo ? history[historyPosition].Description : null;

    public EditorEntityIdentity GetIdentity(Guid editorId) =>
        TryGetIdentity(editorId, out EditorEntityIdentity identity)
            ? identity
            : throw new KeyNotFoundException($"Editor entity '{editorId}' does not belong to this document.");

    public bool TryGetIdentity(Guid editorId, out EditorEntityIdentity identity)
    {
        foreach (EditorEntityIdentity candidate in Identities)
        {
            if (candidate.EditorId == editorId)
            {
                identity = candidate;
                return true;
            }
        }

        identity = default;
        return false;
    }

    public void Execute(IEditorDocumentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (historyPosition < history.Count)
        {
            if (savedPosition > historyPosition) savedPosition = -1;
            history.RemoveRange(historyPosition, history.Count - historyPosition);
        }
        command.Apply(this);
        history.Add(command);
        historyPosition++;
        Revision++;
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        history[--historyPosition].Revert(this);
        Revision++;
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        history[historyPosition++].Apply(this);
        Revision++;
        return true;
    }

    public void MarkSaved(string path, string fingerprint)
    {
        SourcePath = Path.GetFullPath(path);
        SourceFingerprint = fingerprint;
        RequiresSaveAs = false;
        savedPosition = historyPosition;
    }

    private static IReadOnlyList<EditorEntityIdentity> CreateIdentities(GameMap map)
    {
        var result = new List<EditorEntityIdentity>();
        Add(map.StaticBoxes.Count, EditorEntityKind.StaticBox); Add(map.StaticModels.Count, EditorEntityKind.StaticModel);
        Add(map.SpawnPoints.Count, EditorEntityKind.SpawnPoint); Add(map.LootPoints.Count, EditorEntityKind.LootPoint);
        Add(map.Navigation.Waypoints.Count, EditorEntityKind.NavigationWaypoint); Add(map.Navigation.Links.Count, EditorEntityKind.NavigationLink);
        return result;
        void Add(int count, EditorEntityKind kind) { for (int i = 0; i < count; i++) result.Add(new(Guid.NewGuid(), kind, i)); }
    }
}
