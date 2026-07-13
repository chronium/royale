using Royale.Content.Maps;

namespace Royale.Editor.Documents;

public sealed class EditorMapDocument
{
    private readonly List<IEditorDocumentCommand> history = [];
    private readonly List<EditorEntityIdentity> identities;
    private int historyPosition;
    private int savedPosition;

    public EditorMapDocument(GameMap map, string? sourcePath, string? sourceFingerprint, bool requiresSaveAs)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        SourcePath = sourcePath;
        SourceFingerprint = sourceFingerprint;
        RequiresSaveAs = requiresSaveAs;
        identities = CreateIdentities(map);
    }

    public GameMap Map { get; internal set; }
    public string? SourcePath { get; private set; }
    public string? SourceFingerprint { get; private set; }
    public bool RequiresSaveAs { get; private set; }
    public IReadOnlyList<EditorEntityIdentity> Identities => identities;
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

    internal void InsertEntity(EditorEntityKind kind, int index, object definition, Guid editorId)
    {
        if (editorId == Guid.Empty)
            throw new ArgumentException("An editor identity is required.", nameof(editorId));
        if (identities.Any(identity => identity.EditorId == editorId))
            throw new ArgumentException($"Editor identity '{editorId}' already exists.", nameof(editorId));

        InsertDefinition(kind, index, definition);
        for (int identityIndex = 0; identityIndex < identities.Count; identityIndex++)
        {
            EditorEntityIdentity identity = identities[identityIndex];
            if (identity.Kind == kind && identity.Index >= index)
                identities[identityIndex] = identity with { Index = identity.Index + 1 };
        }
        identities.Add(new EditorEntityIdentity(editorId, kind, index));
        ReindexIdentities();
    }

    internal object RemoveEntity(Guid editorId)
    {
        EditorEntityIdentity identity = GetIdentity(editorId);
        object definition = RemoveDefinition(identity.Kind, identity.Index);
        identities.RemoveAll(candidate => candidate.EditorId == editorId);
        for (int identityIndex = 0; identityIndex < identities.Count; identityIndex++)
        {
            EditorEntityIdentity candidate = identities[identityIndex];
            if (candidate.Kind == identity.Kind && candidate.Index > identity.Index)
                identities[identityIndex] = candidate with { Index = candidate.Index - 1 };
        }
        ReindexIdentities();
        return definition;
    }

    internal object GetDefinition(Guid editorId)
    {
        EditorEntityIdentity identity = GetIdentity(editorId);
        return identity.Kind switch
        {
            EditorEntityKind.StaticBox => Map.StaticBoxes[identity.Index],
            EditorEntityKind.StaticModel => Map.StaticModels[identity.Index],
            EditorEntityKind.SpawnPoint => Map.SpawnPoints[identity.Index],
            EditorEntityKind.LootPoint => Map.LootPoints[identity.Index],
            EditorEntityKind.NavigationWaypoint => Map.Navigation.Waypoints[identity.Index],
            EditorEntityKind.NavigationLink => Map.Navigation.Links[identity.Index],
            _ => throw new ArgumentOutOfRangeException(nameof(editorId)),
        };
    }

    internal void ReplaceDefinition(Guid editorId, object definition)
    {
        EditorEntityIdentity identity = GetIdentity(editorId);
        switch (identity.Kind)
        {
            case EditorEntityKind.StaticBox:
                Map.StaticBoxes[identity.Index] = Require<StaticBoxDefinition>(definition, identity.Kind);
                break;
            case EditorEntityKind.StaticModel:
                Map.StaticModels[identity.Index] = Require<StaticModelDefinition>(definition, identity.Kind);
                break;
            case EditorEntityKind.SpawnPoint:
                Map.SpawnPoints[identity.Index] = Require<MapSpawnPoint>(definition, identity.Kind);
                break;
            case EditorEntityKind.LootPoint:
                Map.LootPoints[identity.Index] = Require<MapLootPoint>(definition, identity.Kind);
                break;
            case EditorEntityKind.NavigationWaypoint:
                Map.Navigation.Waypoints[identity.Index] = Require<MapNavigationWaypoint>(definition, identity.Kind);
                break;
            case EditorEntityKind.NavigationLink:
                Map.Navigation.Links[identity.Index] = Require<MapNavigationLink>(definition, identity.Kind);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(editorId));
        }
    }

    private void ReindexIdentities()
    {
        identities.Sort((left, right) =>
        {
            int kind = left.Kind.CompareTo(right.Kind);
            return kind != 0 ? kind : left.Index.CompareTo(right.Index);
        });
        foreach (EditorEntityKind kind in Enum.GetValues<EditorEntityKind>())
        {
            int index = 0;
            for (int identityIndex = 0; identityIndex < identities.Count; identityIndex++)
            {
                if (identities[identityIndex].Kind == kind)
                    identities[identityIndex] = identities[identityIndex] with { Index = index++ };
            }
        }
    }

    private void InsertDefinition(EditorEntityKind kind, int index, object definition)
    {
        switch (kind)
        {
            case EditorEntityKind.StaticBox:
                Map.StaticBoxes.Insert(index, Require<StaticBoxDefinition>(definition, kind));
                break;
            case EditorEntityKind.StaticModel:
                Map.StaticModels.Insert(index, Require<StaticModelDefinition>(definition, kind));
                break;
            case EditorEntityKind.SpawnPoint:
                Map.SpawnPoints.Insert(index, Require<MapSpawnPoint>(definition, kind));
                break;
            case EditorEntityKind.LootPoint:
                Map.LootPoints.Insert(index, Require<MapLootPoint>(definition, kind));
                break;
            case EditorEntityKind.NavigationWaypoint:
                Map.Navigation.Waypoints.Insert(index, Require<MapNavigationWaypoint>(definition, kind));
                break;
            case EditorEntityKind.NavigationLink:
                Map.Navigation.Links.Insert(index, Require<MapNavigationLink>(definition, kind));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private object RemoveDefinition(EditorEntityKind kind, int index)
    {
        return kind switch
        {
            EditorEntityKind.StaticBox => RemoveAt(Map.StaticBoxes, index),
            EditorEntityKind.StaticModel => RemoveAt(Map.StaticModels, index),
            EditorEntityKind.SpawnPoint => RemoveAt(Map.SpawnPoints, index),
            EditorEntityKind.LootPoint => RemoveAt(Map.LootPoints, index),
            EditorEntityKind.NavigationWaypoint => RemoveAt(Map.Navigation.Waypoints, index),
            EditorEntityKind.NavigationLink => RemoveAt(Map.Navigation.Links, index),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static T RemoveAt<T>(List<T> values, int index)
    {
        T value = values[index];
        values.RemoveAt(index);
        return value;
    }

    private static T Require<T>(object definition, EditorEntityKind kind) where T : class =>
        definition as T ?? throw new ArgumentException($"Definition does not match entity kind '{kind}'.", nameof(definition));

    private static List<EditorEntityIdentity> CreateIdentities(GameMap map)
    {
        var result = new List<EditorEntityIdentity>();
        Add(map.StaticBoxes.Count, EditorEntityKind.StaticBox); Add(map.StaticModels.Count, EditorEntityKind.StaticModel);
        Add(map.SpawnPoints.Count, EditorEntityKind.SpawnPoint); Add(map.LootPoints.Count, EditorEntityKind.LootPoint);
        Add(map.Navigation.Waypoints.Count, EditorEntityKind.NavigationWaypoint); Add(map.Navigation.Links.Count, EditorEntityKind.NavigationLink);
        return result;
        void Add(int count, EditorEntityKind kind) { for (int i = 0; i < count; i++) result.Add(new(Guid.NewGuid(), kind, i)); }
    }
}
