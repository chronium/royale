using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Royale.Editor.Mcp;

[McpServerToolType]
public sealed class EditorMcpTools
{
    private readonly EditorMainThreadDispatcher dispatcher;
    private readonly EditorMcpWorkspace workspace;

    public EditorMcpTools(EditorMainThreadDispatcher dispatcher, EditorMcpWorkspace workspace)
    {
        this.dispatcher = dispatcher;
        this.workspace = workspace;
    }

    [McpServerTool(Name = "get_editor_status", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the active editor document, revision, history, selection, and manifest status.")]
    public Task<EditorMcpStatusResult> GetEditorStatus(CancellationToken cancellationToken) =>
        Invoke(workspace.GetStatus, cancellationToken);

    [McpServerTool(Name = "get_map", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the active map identity, settings, bounds, safe zone, and entity counts.")]
    public Task<EditorMcpMapResult> GetMap(CancellationToken cancellationToken) =>
        Invoke(workspace.GetMap, cancellationToken);

    [McpServerTool(Name = "list_assets", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists model assets and their relative render, resource, and collision metadata.")]
    public Task<EditorMcpAssetListResult> ListAssets(CancellationToken cancellationToken) =>
        Invoke(workspace.ListAssets, cancellationToken);

    [McpServerTool(Name = "list_entities", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists stable editor entity identities, optionally filtered by entity kind.")]
    public Task<EditorMcpEntityListResult> ListEntities(
        [Description("Optional kind: staticBox, staticModel, spawnPoint, lootPoint, navigationWaypoint, or navigationLink.")] string? kind,
        CancellationToken cancellationToken) => Invoke(() => workspace.ListEntities(kind), cancellationToken);

    [McpServerTool(Name = "get_entity", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets one complete kind-specific entity definition, transform, and transform capabilities.")]
    public Task<EditorMcpEntityResult> GetEntity(
        [Description("Stable editor GUID from list_entities.")] Guid editorId,
        CancellationToken cancellationToken) => Invoke(() => workspace.GetEntity(editorId), cancellationToken);

    [McpServerTool(Name = "validate_map", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Runs runtime-equivalent validation for the active committed revision and updates the Validation panel.")]
    public Task<EditorMcpValidationResult> ValidateMap(
        [Description("Committed document revision that must still be current.")] long expectedRevision,
        CancellationToken cancellationToken) => Invoke(() => workspace.ValidateMap(expectedRevision), cancellationToken);

    [McpServerTool(Name = "set_map_name", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Changes the active map display name as one undoable command.")]
    public Task<EditorMcpMutationResult> SetMapName(long expectedRevision, string name, CancellationToken cancellationToken) =>
        Invoke(() => workspace.SetMapName(expectedRevision, name), cancellationToken);

    [McpServerTool(Name = "set_world_bounds", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Replaces the complete active map world bounds as one undoable command.")]
    public Task<EditorMcpMutationResult> SetWorldBounds(long expectedRevision, EditorMcpBounds bounds, CancellationToken cancellationToken) =>
        Invoke(() => workspace.SetWorldBounds(expectedRevision, bounds), cancellationToken);

    [McpServerTool(Name = "set_safe_zone", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Replaces the complete active map safe zone as one undoable command.")]
    public Task<EditorMcpMutationResult> SetSafeZone(long expectedRevision, EditorMcpSafeZone safeZone, CancellationToken cancellationToken) =>
        Invoke(() => workspace.SetSafeZone(expectedRevision, safeZone), cancellationToken);

    [McpServerTool(Name = "create_entity", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Appends a validated entity definition to its map collection and returns its stable editor GUID.")]
    public Task<EditorMcpEntityMutationResult> CreateEntity(long expectedRevision, EditorMcpEntityDefinition definition, CancellationToken cancellationToken) =>
        Invoke(() => workspace.CreateEntity(expectedRevision, definition), cancellationToken);

    [McpServerTool(Name = "duplicate_entity", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Duplicates an entity after its source using the editor's unique-ID rules.")]
    public Task<EditorMcpEntityMutationResult> DuplicateEntity(long expectedRevision, Guid editorId, CancellationToken cancellationToken) =>
        Invoke(() => workspace.DuplicateEntity(expectedRevision, editorId), cancellationToken);

    [McpServerTool(Name = "replace_entity", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Replaces one entity with a complete same-kind definition as one undoable command.")]
    public Task<EditorMcpEntityMutationResult> ReplaceEntity(long expectedRevision, Guid editorId, EditorMcpEntityDefinition definition, CancellationToken cancellationToken) =>
        Invoke(() => workspace.ReplaceEntity(expectedRevision, editorId, definition), cancellationToken);

    [McpServerTool(Name = "set_entity_transform", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Replaces an entity's complete transform and rejects changes to unsupported components.")]
    public Task<EditorMcpEntityMutationResult> SetEntityTransform(long expectedRevision, Guid editorId, EditorMcpTransform transform, CancellationToken cancellationToken) =>
        Invoke(() => workspace.SetEntityTransform(expectedRevision, editorId, transform), cancellationToken);

    [McpServerTool(Name = "delete_entity", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Deletes an entity; waypoint deletion also removes incident navigation links.")]
    public Task<EditorMcpDeleteResult> DeleteEntity(long expectedRevision, Guid editorId, CancellationToken cancellationToken) =>
        Invoke(() => workspace.DeleteEntity(expectedRevision, editorId), cancellationToken);

    [McpServerTool(Name = "snap_entity_to_face", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Snaps a spatial entity against the first collision face hit by a world-space ray; a miss is successful and unchanged.")]
    public Task<EditorMcpFaceSnapResult> SnapEntityToFace(
        long expectedRevision,
        Guid editorId,
        EditorMcpVector3 rayOrigin,
        EditorMcpVector3 rayDirection,
        float maximumDistance,
        bool alignmentEnabled,
        EditorMcpFaceSnapAxis alignmentAxis,
        CancellationToken cancellationToken) => Invoke(
            () => workspace.SnapEntityToFace(
                expectedRevision,
                editorId,
                rayOrigin,
                rayDirection,
                maximumDistance,
                alignmentEnabled,
                alignmentAxis),
            cancellationToken);

    [McpServerTool(Name = "undo", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Undoes the latest editor document command when history is available.")]
    public Task<EditorMcpMutationResult> Undo(long expectedRevision, CancellationToken cancellationToken) =>
        Invoke(() => workspace.Undo(expectedRevision), cancellationToken);

    [McpServerTool(Name = "redo", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Redoes the next editor document command when history is available.")]
    public Task<EditorMcpMutationResult> Redo(long expectedRevision, CancellationToken cancellationToken) =>
        Invoke(() => workspace.Redo(expectedRevision), cancellationToken);

    [McpServerTool(Name = "save", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Validates and saves the active project or standalone map to its existing destination.")]
    public Task<EditorMcpSaveResult> Save(long expectedRevision, CancellationToken cancellationToken) =>
        Invoke(() => workspace.Save(expectedRevision), cancellationToken);

    private async Task<T> Invoke<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await dispatcher.DispatchAsync(operation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            InvalidOperationException or
            KeyNotFoundException or
            IOException)
        {
            throw new McpException(exception.Message);
        }
    }
}
