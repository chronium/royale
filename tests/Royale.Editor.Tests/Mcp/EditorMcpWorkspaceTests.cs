using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Editor.Documents;
using Royale.Editor.Mcp;
using Royale.Editor.Persistence;
using Royale.Editor.Tests.Infrastructure;
using Royale.Rendering.Meshes;

namespace Royale.Editor.Tests.Mcp;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class EditorMcpWorkspaceTests
{
    [Fact]
    public void RejectsBusyAndStaleRequestsWithoutChangingRevision()
    {
        EditorMapDocument document = CreateDocument();
        bool busy = true;
        EditorMcpWorkspace workspace = CreateWorkspace(document, isBusy: () => busy);

        InvalidOperationException busyError = Assert.Throws<InvalidOperationException>(workspace.GetMap);
        Assert.Contains("busy", busyError.Message);
        busy = false;

        InvalidOperationException stale = Assert.Throws<InvalidOperationException>(() =>
            workspace.SetMapName(99, "Changed"));
        Assert.Contains("expected 99, current 0", stale.Message);
        Assert.Equal(0, document.Revision);
        Assert.Equal("Map", document.Map.Name);
    }

    [Fact]
    public void NoOpMutationsDoNotIncrementRevisionAndUnsupportedTransformChangesFail()
    {
        EditorMapDocument document = CreateDocument();
        EditorMcpWorkspace workspace = CreateWorkspace(document);
        EditorEntityIdentity loot = document.Identities.Single(identity => identity.Kind == EditorEntityKind.LootPoint);

        Assert.False(workspace.SetMapName(0, "Map").Changed);
        EditorMcpEntityResult entity = workspace.GetEntity(loot.EditorId);
        Assert.False(workspace.SetEntityTransform(0, loot.EditorId, entity.Transform!).Changed);

        EditorMcpTransform changedRotation = entity.Transform! with
        {
            RotationDegrees = new EditorMcpVector3(0, 10, 0),
        };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            workspace.SetEntityTransform(0, loot.EditorId, changedRotation));
        Assert.Contains("does not support rotation", error.Message);
        Assert.Equal(0, document.Revision);
    }

    [Fact]
    public void CreateDuplicateAndMissingRenderAssetUseExistingRules()
    {
        EditorMapDocument document = CreateDocument();
        EditorMcpWorkspace workspace = CreateWorkspace(document);
        var box = new EditorMcpStaticBoxDefinition(
            "crate-2",
            new EditorMcpVector3(1, 0, 0),
            new EditorMcpVector3(0, 0, 0),
            new EditorMcpVector3(1, 1, 1));

        EditorMcpEntityMutationResult created = workspace.CreateEntity(0, box);
        EditorMcpEntityMutationResult duplicate = workspace.DuplicateEntity(1, created.EditorId);

        Assert.Equal("crate-2-2", document.Map.StaticBoxes[2].Id);
        Assert.Equal(2, duplicate.Revision);
        var missingModel = new EditorMcpStaticModelDefinition(
            "missing-model",
            "missing",
            new EditorMcpVector3(0, 0, 0),
            new EditorMcpVector3(0, 0, 0),
            new EditorMcpVector3(1, 1, 1));
        KeyNotFoundException error = Assert.Throws<KeyNotFoundException>(() =>
            workspace.CreateEntity(2, missingModel));
        Assert.Contains("Render-capable", error.Message);
        Assert.Equal(2, document.Revision);
    }

    [Fact]
    public void WaypointReplaceAndDeletePreserveLinkIntegrityAndSingleRevisions()
    {
        EditorMapDocument document = CreateDocument();
        EditorMcpWorkspace workspace = CreateWorkspace(document);
        EditorEntityIdentity waypoint = document.Identities.Single(identity =>
            identity.Kind == EditorEntityKind.NavigationWaypoint && identity.Index == 0);

        EditorMcpEntityMutationResult replaced = workspace.ReplaceEntity(
            0,
            waypoint.EditorId,
            new EditorMcpNavigationWaypointDefinition("renamed", new EditorMcpVector3(1, 0, 1)));

        Assert.Equal(1, replaced.Revision);
        Assert.Equal("renamed", document.Map.Navigation.Links[0].From);
        Assert.Equal(new MapVector3(1, 0, 1), document.Map.Navigation.Waypoints[0].Position);

        EditorMcpDeleteResult deleted = workspace.DeleteEntity(1, waypoint.EditorId);
        Assert.Equal(1, deleted.RemovedIncidentLinks);
        Assert.Empty(document.Map.Navigation.Links);
        workspace.Undo(2);
        Assert.Equal("renamed", document.Map.Navigation.Links[0].From);
        workspace.Undo(3);
        Assert.Equal("a", document.Map.Navigation.Links[0].From);
        Assert.Equal(4, document.Revision);
    }

    [Fact]
    public void DeleteClearsOnlyDeletedSelection()
    {
        EditorMapDocument document = CreateDocument();
        Guid selected = document.Identities.Single(identity => identity.Kind == EditorEntityKind.LootPoint).EditorId;
        Guid? selection = selected;
        EditorMcpWorkspace workspace = CreateWorkspace(
            document,
            getSelection: () => selection,
            setSelection: value => selection = value);

        workspace.SetMapName(0, "Changed");
        Assert.Equal(selected, selection);
        workspace.DeleteEntity(1, selected);
        Assert.Null(selection);
    }

    [Fact]
    public void SaveRequiresDestinationAndDoesNotIncrementRevision()
    {
        EditorMapDocument unsaved = CreateDocument(requiresSaveAs: true);
        EditorMcpWorkspace unsavedWorkspace = CreateWorkspace(unsaved);
        InvalidOperationException required = Assert.Throws<InvalidOperationException>(() => unsavedWorkspace.Save(0));
        Assert.Contains("Save As is required", required.Message);

        string root = Path.Combine(Path.GetTempPath(), "royale-editor-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "map.json");
            File.WriteAllBytes(path, MapFileSerializer.Serialize(CreateMap()));
            EditorMapDocument document = EditorMapPersistence.Load(path);
            EditorMcpWorkspace workspace = CreateWorkspace(document);
            workspace.SetMapName(0, "Saved Name");

            EditorMcpSaveResult saved = workspace.Save(1);

            Assert.Equal(1, saved.Revision);
            Assert.False(document.IsDirty);
            Assert.Equal("Saved Name", MapCatalog.LoadFile(path).Name);

            File.AppendAllText(path, " ");
            workspace.SetMapName(1, "Conflicting Name");
            IOException conflict = Assert.Throws<IOException>(() => workspace.Save(2));
            Assert.Contains("changed externally", conflict.Message);
            Assert.Equal(2, document.Revision);
            Assert.True(document.IsDirty);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FaceSnapReportsMissAndCommitsCollisionHit()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity loot = document.Identities.Single(identity => identity.Kind == EditorEntityKind.LootPoint);
        ModelAssetManifest manifest = CreateManifest();
        StaticMeshAssetCache cache = StaticMeshAssetCache.LoadSource(Path.GetTempPath(), manifest);
        EditorMcpWorkspace workspace = CreateWorkspace(document, manifest: manifest, meshCache: cache);

        EditorMcpFaceSnapResult miss = workspace.SnapEntityToFace(
            0,
            loot.EditorId,
            new EditorMcpVector3(0, 2, 0),
            new EditorMcpVector3(0, 1, 0),
            10,
            false,
            EditorMcpFaceSnapAxis.PositiveY);
        Assert.False(miss.Hit);
        Assert.False(miss.Changed);
        Assert.Equal(0, document.Revision);

        EditorMcpFaceSnapResult hit = workspace.SnapEntityToFace(
            0,
            loot.EditorId,
            new EditorMcpVector3(0, 2, 0),
            new EditorMcpVector3(0, -1, 0),
            10,
            false,
            EditorMcpFaceSnapAxis.PositiveY);
        Assert.True(hit.Hit);
        Assert.True(hit.Changed);
        Assert.Equal("crate", hit.ColliderContentId);
        Assert.Equal(1, hit.Revision);
    }

    private static EditorMcpWorkspace CreateWorkspace(
        EditorMapDocument document,
        Func<bool>? isBusy = null,
        Func<Guid?>? getSelection = null,
        Action<Guid?>? setSelection = null,
        ModelAssetManifest? manifest = null,
        StaticMeshAssetCache? meshCache = null)
    {
        manifest ??= CreateManifest();
        return new EditorMcpWorkspace(
            () => document,
            () => null,
            () => manifest,
            () => meshCache,
            getSelection ?? (() => null),
            isBusy ?? (() => false),
            setSelection ?? (_ => { }),
            () => { },
            _ => { });
    }

    private static ModelAssetManifest CreateManifest() => new()
    {
        Version = ModelAssetManifest.CurrentVersion,
        Assets =
        [
            new ModelAssetDefinition
            {
                Id = "asset",
                Render = new ModelRenderAssetDefinition { Source = "asset.glb" },
                Collision = new ModelCollisionAssetDefinition(),
            },
        ],
    };

    private static EditorMapDocument CreateDocument(bool requiresSaveAs = false) =>
        new(CreateMap(), null, null, requiresSaveAs);

    private static GameMap CreateMap() => new()
    {
        Id = "map",
        Name = "Map",
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "crate",
                Position = new MapVector3(0, -0.5f, 0),
                Size = new MapVector3(10, 1, 10),
            },
        ],
        SpawnPoints = [new MapSpawnPoint { Id = "spawn", Position = new MapVector3(0, 0, 0) }],
        LootPoints = [new MapLootPoint { Id = "loot", Position = new MapVector3(0, 0, 0) }],
        Navigation = new MapNavigationDefinition
        {
            Waypoints =
            [
                new MapNavigationWaypoint { Id = "a", Position = new MapVector3(-1, 0, 0) },
                new MapNavigationWaypoint { Id = "b", Position = new MapVector3(1, 0, 0) },
            ],
            Links = [new MapNavigationLink { From = "a", To = "b" }],
        },
        WorldBounds = new MapBounds
        {
            Min = new MapVector3(-10, -2, -10),
            Max = new MapVector3(10, 5, 10),
        },
        SafeZone = new SafeZoneDefinition { Center = new MapVector3(0, 0, 0), Radius = 5 },
    };
}
