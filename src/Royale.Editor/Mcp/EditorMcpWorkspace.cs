using System.Numerics;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Editor.Documents;
using Royale.Editor.Persistence;
using Royale.Editor.Projects;
using Royale.Editor.Validation;
using Royale.Editor.Viewport;
using Royale.Editor.Viewport.FaceSnap;
using Royale.Rendering.Meshes;
using Royale.Simulation.World;

namespace Royale.Editor.Mcp;

public sealed class EditorMcpWorkspace
{
    private readonly Func<EditorMapDocument?> getDocument;
    private readonly Func<EditorProjectSession?> getProjectSession;
    private readonly Func<ModelAssetManifest?> getManifest;
    private readonly Func<StaticMeshAssetCache?> getMeshCache;
    private readonly Func<Guid?> getSelection;
    private readonly Func<bool> isPreviewActive;
    private readonly Action<Guid?> setSelection;
    private readonly Action refreshPresentation;
    private readonly Action<EditorMapValidationReport> publishValidation;
    private readonly Func<IModelContactSheetCaptureService?> getContactSheetCaptureService;

    public EditorMcpWorkspace(
        Func<EditorMapDocument?> getDocument,
        Func<EditorProjectSession?> getProjectSession,
        Func<ModelAssetManifest?> getManifest,
        Func<StaticMeshAssetCache?> getMeshCache,
        Func<Guid?> getSelection,
        Func<bool> isPreviewActive,
        Action<Guid?> setSelection,
        Action refreshPresentation,
        Action<EditorMapValidationReport> publishValidation,
        Func<IModelContactSheetCaptureService?>? getContactSheetCaptureService = null)
    {
        this.getDocument = getDocument;
        this.getProjectSession = getProjectSession;
        this.getManifest = getManifest;
        this.getMeshCache = getMeshCache;
        this.getSelection = getSelection;
        this.isPreviewActive = isPreviewActive;
        this.setSelection = setSelection;
        this.refreshPresentation = refreshPresentation;
        this.publishValidation = publishValidation;
        this.getContactSheetCaptureService = getContactSheetCaptureService ?? (() => null);
    }

    public EditorMcpStatusResult GetStatus()
    {
        EditorMapDocument document = RequireDocument();
        EditorEntityIdentity? selected = ResolveSelection(document);
        return new EditorMcpStatusResult(
            getProjectSession() is null ? "standaloneMap" : "project",
            document.SourcePath,
            document.Map.Id,
            document.Map.Name,
            document.Revision,
            document.IsDirty,
            document.CanUndo,
            document.CanRedo,
            document.UndoDescription,
            document.RedoDescription,
            selected is EditorEntityIdentity identity
                ? new EditorMcpSelection(identity.EditorId, KindName(identity.Kind), EditorEntityTransforms.GetDisplayId(document, identity))
                : null,
            ManifestFingerprint());
    }

    public EditorMcpMapResult GetMap()
    {
        EditorMapDocument document = RequireDocument();
        GameMap map = document.Map;
        return new EditorMcpMapResult(
            map.Name,
            map.Id,
            ToContract(map.WorldBounds),
            ToContract(map.SafeZone),
            new EditorMcpEntityCounts(
                map.StaticBoxes.Count,
                map.StaticModels.Count,
                map.SpawnPoints.Count,
                map.LootPoints.Count,
                map.Navigation.Waypoints.Count,
                map.Navigation.Links.Count),
            document.Revision);
    }

    public EditorMcpAssetListResult ListAssets()
    {
        EditorMapDocument document = RequireDocument();
        ModelAssetManifest manifest = getManifest() ?? throw new InvalidOperationException("The active document has no asset manifest.");
        EditorMcpAssetResult[] assets = manifest.Assets
            .OrderBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(asset => new EditorMcpAssetResult(
                asset.Id,
                asset.Render?.Source,
                asset.Render?.Resources ?? [],
                asset.Collision.Mode.ToString(),
                asset.Collision.Source,
                asset.Collision.Artifact,
                asset.Render is not null))
            .ToArray();
        return new EditorMcpAssetListResult(assets, ManifestFingerprint(), document.Revision);
    }

    public EditorMcpEntityListResult ListEntities(string? kind)
    {
        EditorMapDocument document = RequireDocument();
        EditorEntityKind? filter = kind is null ? null : ParseKind(kind);
        EditorMcpEntitySummary[] entities = document.Identities
            .Where(identity => filter is null || identity.Kind == filter)
            .Select(identity => new EditorMcpEntitySummary(
                identity.EditorId,
                KindName(identity.Kind),
                EditorEntityTransforms.GetDisplayId(document, identity),
                identity.Index))
            .ToArray();
        return new EditorMcpEntityListResult(entities, document.Revision);
    }

    public EditorMcpEntityResult GetEntity(Guid editorId)
    {
        EditorMapDocument document = RequireDocument();
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        EditorTransformCapabilities capabilities = EditorEntityTransforms.GetCapabilities(identity.Kind);
        return new EditorMcpEntityResult(
            editorId,
            ToContract(document.GetDefinition(editorId)),
            capabilities == EditorTransformCapabilities.None
                ? null
                : ToContract(EditorEntityTransforms.Get(document, identity)),
            ToContract(capabilities),
            document.Revision);
    }

    public EditorMcpValidationResult ValidateMap(long expectedRevision)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        using EditorMapValidationResult validation = EditorMapValidator.Validate(document, getProjectSession());
        publishValidation(validation.Report);
        return new EditorMcpValidationResult(
            validation.Report.Success,
            validation.Report.DocumentRevision,
            validation.Report.AssetManifestFingerprint,
            validation.Report.Stages.Select(stage =>
                new EditorMcpValidationStage(stage.Category, stage.Success, stage.Message)).ToArray());
    }

    public Task<ModelContactSheetCapture> CaptureModelContactSheets(
        string assetId,
        string? viewSet,
        CancellationToken cancellationToken)
    {
        IModelContactSheetCaptureService service = getContactSheetCaptureService()
            ?? throw new InvalidOperationException("Model contact-sheet capture is unavailable because rendering is not initialized.");
        return service.CaptureAsync(assetId, viewSet, cancellationToken);
    }

    public EditorMcpMutationResult SetMapName(long expectedRevision, string name)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Map name must be non-empty.", nameof(name));
        if (string.Equals(document.Map.Name, name, StringComparison.Ordinal))
            return Unchanged(document);
        document.Execute(new SetMapNameCommand(document.Map.Name, name));
        Refresh();
        return Changed(document);
    }

    public EditorMcpMutationResult SetWorldBounds(long expectedRevision, EditorMcpBounds bounds)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        MapBounds value = ToMap(bounds);
        EditorMapEditing.ValidateBounds(value);
        if (Equals(document.Map.WorldBounds, value))
            return Unchanged(document);
        document.Execute(new SetWorldBoundsCommand(document.Map.WorldBounds, value));
        Refresh();
        return Changed(document);
    }

    public EditorMcpMutationResult SetSafeZone(long expectedRevision, EditorMcpSafeZone safeZone)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        SafeZoneDefinition value = ToMap(safeZone);
        EditorMapEditing.ValidateSafeZone(value);
        if (Equals(document.Map.SafeZone, value))
            return Unchanged(document);
        document.Execute(new SetSafeZoneCommand(document.Map.SafeZone, value));
        Refresh();
        return Changed(document);
    }

    public EditorMcpEntityMutationResult CreateEntity(long expectedRevision, EditorMcpEntityDefinition definition)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        (EditorEntityKind kind, object value) = ToMap(definition);
        ValidateDefinitionAndAsset(document, kind, value);
        int index = Count(document.Map, kind);
        var command = new AddEntityCommand(kind, index, value);
        document.Execute(command);
        Refresh();
        return new EditorMcpEntityMutationResult(true, document.Revision, command.EditorId);
    }

    public EditorMcpEntityMutationResult DuplicateEntity(long expectedRevision, Guid editorId)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        object value = EditorMapEditing.DuplicateDefinition(document, identity);
        var command = new AddEntityCommand(identity.Kind, identity.Index + 1, value);
        document.Execute(command);
        Refresh();
        return new EditorMcpEntityMutationResult(true, document.Revision, command.EditorId);
    }

    public EditorMcpEntityMutationResult ReplaceEntity(
        long expectedRevision,
        Guid editorId,
        EditorMcpEntityDefinition definition)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        (EditorEntityKind kind, object after) = ToMap(definition);
        if (kind != identity.Kind)
            throw new ArgumentException($"Replacement kind '{KindName(kind)}' does not match entity kind '{KindName(identity.Kind)}'.");
        ValidateDefinitionAndAsset(document, kind, after, editorId);
        object before = document.GetDefinition(editorId);
        if (Equals(before, after))
            return new EditorMcpEntityMutationResult(false, document.Revision, editorId);

        if (before is MapNavigationWaypoint oldWaypoint && after is MapNavigationWaypoint newWaypoint &&
            !string.Equals(oldWaypoint.Id, newWaypoint.Id, StringComparison.Ordinal))
        {
            MapNavigationWaypoint renamedBefore = oldWaypoint with { Id = newWaypoint.Id };
            document.Execute(new WaypointReplacementCommand(
                new RenameWaypointCommand(editorId, oldWaypoint.Id, newWaypoint.Id),
                new ReplaceEntityCommand(editorId, renamedBefore, newWaypoint)));
        }
        else
        {
            document.Execute(new ReplaceEntityCommand(editorId, before, after));
        }
        Refresh();
        return new EditorMcpEntityMutationResult(true, document.Revision, editorId);
    }

    public EditorMcpEntityMutationResult SetEntityTransform(
        long expectedRevision,
        Guid editorId,
        EditorMcpTransform transform)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        EditorTransformCapabilities capabilities = EditorEntityTransforms.GetCapabilities(identity.Kind);
        if (capabilities == EditorTransformCapabilities.None)
            throw new InvalidOperationException("Navigation links do not support transforms.");
        EditorEntityTransform before = EditorEntityTransforms.Get(document, identity);
        EditorEntityTransform after = ToEditor(transform);
        RequireUnsupportedComponentsUnchanged(capabilities, before, after);
        EditorEntityTransforms.ValidateTransform(identity.Kind, after);
        if (before.NearlyEquals(after))
            return new EditorMcpEntityMutationResult(false, document.Revision, editorId);
        document.Execute(new SetEntityTransformCommand(editorId, before, after));
        Refresh();
        return new EditorMcpEntityMutationResult(true, document.Revision, editorId);
    }

    public EditorMcpDeleteResult DeleteEntity(long expectedRevision, Guid editorId)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        _ = document.GetIdentity(editorId);
        var command = new RemoveEntityCommand(editorId);
        document.Execute(command);
        if (getSelection() == editorId)
            setSelection(null);
        Refresh();
        return new EditorMcpDeleteResult(true, document.Revision, command.IncidentLinkCount);
    }

    public EditorMcpMutationResult Undo(long expectedRevision)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        if (!document.Undo())
            return Unchanged(document);
        if (getSelection() is Guid selected && !document.TryGetIdentity(selected, out _))
            setSelection(null);
        Refresh();
        return Changed(document);
    }

    public EditorMcpMutationResult Redo(long expectedRevision)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        if (!document.Redo())
            return Unchanged(document);
        if (getSelection() is Guid selected && !document.TryGetIdentity(selected, out _))
            setSelection(null);
        Refresh();
        return Changed(document);
    }

    public EditorMcpSaveResult Save(long expectedRevision)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        if (document.RequiresSaveAs || document.SourcePath is null)
            throw new InvalidOperationException("The active map has no writable in-place destination; Save As is required.");
        EditorProjectSession? project = getProjectSession();
        if (project is not null)
            project.Save();
        else
            EditorMapPersistence.Save(document, document.SourcePath, checkExternalChange: true);
        return new EditorMcpSaveResult(true, document.SourcePath!, document.Revision);
    }

    public EditorMcpFaceSnapResult SnapEntityToFace(
        long expectedRevision,
        Guid editorId,
        EditorMcpVector3 rayOrigin,
        EditorMcpVector3 rayDirection,
        float maximumDistance,
        bool alignmentEnabled,
        EditorMcpFaceSnapAxis alignmentAxis)
    {
        EditorMapDocument document = RequireMutation(expectedRevision);
        EditorEntityIdentity identity = document.GetIdentity(editorId);
        if (!EditorEntityTransforms.HasSpatialTransform(identity.Kind))
            throw new InvalidOperationException("Navigation links do not support face snapping.");
        Vector3 direction = ToVector(rayDirection);
        if (!IsFinite(direction) || direction.LengthSquared() <= 0.0f)
            throw new ArgumentException("Ray direction must be finite and non-zero.", nameof(rayDirection));
        if (!IsFinite(ToVector(rayOrigin)))
            throw new ArgumentException("Ray origin must be finite.", nameof(rayOrigin));
        if (!float.IsFinite(maximumDistance) || maximumDistance <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(maximumDistance), "Maximum distance must be positive and finite.");

        StaticMeshAssetCache cache = getMeshCache() ?? throw new InvalidOperationException("The active map has no loaded mesh cache.");
        EditorPickTarget bounds = EditorViewportPresentationBuilder.CreatePickTarget(document, cache, identity);
        MapStaticCollisionWorld collisionWorld = EditorFaceSnapCollisionWorldFactory.Create(document, getProjectSession());
        using var session = new EditorFaceSnapSession(document, identity, bounds, collisionWorld);
        var settings = new EditorFaceSnapSettings(
            alignmentEnabled,
            (EditorFaceSnapAxis)alignmentAxis);
        bool hit = session.TryPreview(
            new EditorRay(ToVector(rayOrigin), direction),
            settings,
            maximumDistance);
        if (!hit)
            return new EditorMcpFaceSnapResult(false, document.Revision, false, null, null, null, null, null, null);

        MapStaticRayHit metadata = session.Hit!.Value;
        bool changed = session.Commit();
        if (changed)
            Refresh();
        return new EditorMcpFaceSnapResult(
            changed,
            document.Revision,
            true,
            ToContract(metadata.Point),
            ToContract(metadata.Normal),
            metadata.Fraction,
            metadata.Collider.ContentId,
            metadata.Collider.Kind.ToString(),
            metadata.Collider.AssetId);
    }

    private EditorMapDocument RequireDocument()
    {
        if (isPreviewActive())
            throw new InvalidOperationException("The editor is busy with an interactive transform or face-snap preview.");
        return getDocument() ?? throw new InvalidOperationException("The editor has no active map document.");
    }

    private EditorMapDocument RequireMutation(long expectedRevision)
    {
        EditorMapDocument document = RequireDocument();
        if (document.Revision != expectedRevision)
            throw new InvalidOperationException(
                $"Stale document revision: expected {expectedRevision}, current {document.Revision}.");
        return document;
    }

    private void Refresh() => refreshPresentation();

    private string? ManifestFingerprint() => getProjectSession()?.AssetManifestFingerprint;

    private EditorEntityIdentity? ResolveSelection(EditorMapDocument document) =>
        getSelection() is Guid editorId && document.TryGetIdentity(editorId, out EditorEntityIdentity identity)
            ? identity
            : null;

    private void ValidateDefinitionAndAsset(
        EditorMapDocument document,
        EditorEntityKind kind,
        object definition,
        Guid? replacedEditorId = null)
    {
        EditorMapEditing.ValidateDefinition(document, kind, definition, replacedEditorId);
        if (definition is not StaticModelDefinition model)
            return;
        ModelAssetDefinition? asset = getManifest()?.Assets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, model.AssetId, StringComparison.Ordinal));
        if (asset?.Render is null)
            throw new KeyNotFoundException($"Render-capable model asset '{model.AssetId}' was not found in the active manifest.");
    }

    private static void RequireUnsupportedComponentsUnchanged(
        EditorTransformCapabilities capabilities,
        EditorEntityTransform before,
        EditorEntityTransform after)
    {
        if (!capabilities.HasFlag(EditorTransformCapabilities.Rotate) &&
            Vector3.DistanceSquared(before.RotationDegrees, after.RotationDegrees) > 0.00000001f)
            throw new InvalidOperationException("This entity does not support rotation changes.");
        if (!capabilities.HasFlag(EditorTransformCapabilities.Scale) &&
            Vector3.DistanceSquared(before.ScaleOrSize, after.ScaleOrSize) > 0.00000001f)
            throw new InvalidOperationException("This entity does not support scale or size changes.");
    }

    private static (EditorEntityKind Kind, object Value) ToMap(EditorMcpEntityDefinition definition) => definition switch
    {
        EditorMcpStaticBoxDefinition value => (EditorEntityKind.StaticBox, new StaticBoxDefinition
        {
            Id = value.Id,
            Position = ToMap(value.Position),
            RotationEuler = ToMap(value.RotationDegrees),
            Size = ToMap(value.Size),
        }),
        EditorMcpStaticModelDefinition value => (EditorEntityKind.StaticModel, new StaticModelDefinition
        {
            Id = value.Id,
            AssetId = value.AssetId,
            Position = ToMap(value.Position),
            RotationEuler = ToMap(value.RotationDegrees),
            Scale = ToMap(value.Scale),
        }),
        EditorMcpSpawnPointDefinition value => (EditorEntityKind.SpawnPoint, new MapSpawnPoint
        {
            Id = value.Id,
            Position = ToMap(value.Position),
            RotationEuler = ToMap(value.RotationDegrees),
        }),
        EditorMcpLootPointDefinition value => (EditorEntityKind.LootPoint, new MapLootPoint
        {
            Id = value.Id,
            Position = ToMap(value.Position),
        }),
        EditorMcpNavigationWaypointDefinition value => (EditorEntityKind.NavigationWaypoint, new MapNavigationWaypoint
        {
            Id = value.Id,
            Position = ToMap(value.Position),
        }),
        EditorMcpNavigationLinkDefinition value => (EditorEntityKind.NavigationLink, new MapNavigationLink
        {
            From = value.From,
            To = value.To,
        }),
        null => throw new ArgumentNullException(nameof(definition)),
        _ => throw new ArgumentException("Unknown entity definition kind.", nameof(definition)),
    };

    private static EditorMcpEntityDefinition ToContract(object definition) => definition switch
    {
        StaticBoxDefinition value => new EditorMcpStaticBoxDefinition(
            value.Id, ToContract(value.Position), ToContract(value.RotationEuler), ToContract(value.Size)),
        StaticModelDefinition value => new EditorMcpStaticModelDefinition(
            value.Id, value.AssetId, ToContract(value.Position), ToContract(value.RotationEuler), ToContract(value.Scale)),
        MapSpawnPoint value => new EditorMcpSpawnPointDefinition(
            value.Id, ToContract(value.Position), ToContract(value.RotationEuler)),
        MapLootPoint value => new EditorMcpLootPointDefinition(value.Id, ToContract(value.Position)),
        MapNavigationWaypoint value => new EditorMcpNavigationWaypointDefinition(value.Id, ToContract(value.Position)),
        MapNavigationLink value => new EditorMcpNavigationLinkDefinition(value.From, value.To),
        _ => throw new ArgumentException("Unsupported entity definition.", nameof(definition)),
    };

    private static EditorEntityKind ParseKind(string value) => value switch
    {
        "staticBox" => EditorEntityKind.StaticBox,
        "staticModel" => EditorEntityKind.StaticModel,
        "spawnPoint" => EditorEntityKind.SpawnPoint,
        "lootPoint" => EditorEntityKind.LootPoint,
        "navigationWaypoint" => EditorEntityKind.NavigationWaypoint,
        "navigationLink" => EditorEntityKind.NavigationLink,
        _ => throw new ArgumentException($"Unknown entity kind '{value}'.", nameof(value)),
    };

    private static string KindName(EditorEntityKind kind) => kind switch
    {
        EditorEntityKind.StaticBox => "staticBox",
        EditorEntityKind.StaticModel => "staticModel",
        EditorEntityKind.SpawnPoint => "spawnPoint",
        EditorEntityKind.LootPoint => "lootPoint",
        EditorEntityKind.NavigationWaypoint => "navigationWaypoint",
        EditorEntityKind.NavigationLink => "navigationLink",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static int Count(GameMap map, EditorEntityKind kind) => kind switch
    {
        EditorEntityKind.StaticBox => map.StaticBoxes.Count,
        EditorEntityKind.StaticModel => map.StaticModels.Count,
        EditorEntityKind.SpawnPoint => map.SpawnPoints.Count,
        EditorEntityKind.LootPoint => map.LootPoints.Count,
        EditorEntityKind.NavigationWaypoint => map.Navigation.Waypoints.Count,
        EditorEntityKind.NavigationLink => map.Navigation.Links.Count,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static EditorMcpTransformCapabilities ToContract(EditorTransformCapabilities value) => new(
        value.HasFlag(EditorTransformCapabilities.Translate),
        value.HasFlag(EditorTransformCapabilities.Rotate),
        value.HasFlag(EditorTransformCapabilities.Scale));

    private static EditorMcpTransform ToContract(EditorEntityTransform value) => new(
        ToContract(value.Position),
        ToContract(value.RotationDegrees),
        ToContract(value.ScaleOrSize));

    private static EditorEntityTransform ToEditor(EditorMcpTransform value) => new(
        ToVector(value.Position),
        ToVector(value.RotationDegrees),
        ToVector(value.ScaleOrSize));

    private static EditorMcpBounds ToContract(MapBounds value) =>
        new(ToContract(value.Min), ToContract(value.Max));

    private static EditorMcpSafeZone ToContract(SafeZoneDefinition value) =>
        new(ToContract(value.Center), value.Radius);

    private static MapBounds ToMap(EditorMcpBounds value) =>
        new() { Min = ToMap(value.Min), Max = ToMap(value.Max) };

    private static SafeZoneDefinition ToMap(EditorMcpSafeZone value) =>
        new() { Center = ToMap(value.Center), Radius = value.Radius };

    private static EditorMcpVector3 ToContract(MapVector3 value) => new(value.X, value.Y, value.Z);

    private static EditorMcpVector3 ToContract(Vector3 value) => new(value.X, value.Y, value.Z);

    private static MapVector3 ToMap(EditorMcpVector3 value) => new(value.X, value.Y, value.Z);

    private static Vector3 ToVector(EditorMcpVector3 value) => new(value.X, value.Y, value.Z);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static EditorMcpMutationResult Changed(EditorMapDocument document) => new(true, document.Revision);

    private static EditorMcpMutationResult Unchanged(EditorMapDocument document) => new(false, document.Revision);

    private sealed class WaypointReplacementCommand : IEditorDocumentCommand
    {
        private readonly RenameWaypointCommand rename;
        private readonly ReplaceEntityCommand replace;

        public WaypointReplacementCommand(RenameWaypointCommand rename, ReplaceEntityCommand replace)
        {
            this.rename = rename;
            this.replace = replace;
        }

        public string Description => "Edit navigation waypoint";

        public void Apply(EditorMapDocument document)
        {
            rename.Apply(document);
            replace.Apply(document);
        }

        public void Revert(EditorMapDocument document)
        {
            replace.Revert(document);
            rename.Revert(document);
        }
    }
}
