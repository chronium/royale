using Royale.Content;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Projects;
using Royale.Editor.Validation;
using Royale.Editor.Tests.Infrastructure;

namespace Royale.Editor.Tests.Validation;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class EditorMapValidatorTests
{
    [Fact]
    public void StandalonePackagedMapPassesEveryRuntimeStage()
    {
        EditorMapDocument document = CreateDocument(MapCatalog.LoadById(ContentCatalog.DefaultMapId));

        using EditorMapValidationResult result = EditorMapValidator.Validate(document, projectSession: null);

        Assert.True(result.Report.Success, JoinFailures(result.Report));
        Assert.All(result.Report.Stages, stage => Assert.True(stage.Success, stage.Message));
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "assets"), result.ClientAssetRoot);
        Assert.Equal(result.ClientAssetRoot, result.ServerAssetRoot);
    }

    [Fact]
    public void InvalidBoundsAreReportedByMapCategory()
    {
        GameMap map = MapCatalog.LoadById(ContentCatalog.DefaultMapId) with
        {
            WorldBounds = new MapBounds
            {
                Min = new MapVector3(2, 0, 0),
                Max = new MapVector3(1, 1, 1),
            },
        };

        using EditorMapValidationResult result = EditorMapValidator.Validate(CreateDocument(map), null);

        EditorMapValidationStage stage = Assert.Single(
            result.Report.Stages,
            candidate => candidate.Category == "Map schema and bounds");
        Assert.False(stage.Success);
        Assert.Contains("worldBounds", stage.Message);
    }

    [Fact]
    public void MissingReferencedModelFailsRenderAndCollisionCategories()
    {
        GameMap source = MapCatalog.LoadById("prototype-arena");
        StaticModelDefinition model = source.StaticModels[0] with { AssetId = "missing-model" };
        GameMap map = source with { StaticModels = [model, .. source.StaticModels.Skip(1)] };

        using EditorMapValidationResult result = EditorMapValidator.Validate(CreateDocument(map), null);

        Assert.False(result.Report.Success);
        Assert.False(result.Report.Stages.Single(stage => stage.Category == "Referenced render assets").Success);
        Assert.False(result.Report.Stages.Single(stage => stage.Category == "Server collision world").Success);
    }

    [Fact]
    public void SpawnOverlapAndInitialSafeZoneViolationsAreLaunchBlocking()
    {
        GameMap source = MapCatalog.LoadById(ContentCatalog.DefaultMapId);
        MapSpawnPoint first = source.SpawnPoints[0];
        MapSpawnPoint second = source.SpawnPoints[1] with { Position = first.Position };
        GameMap overlap = source with { SpawnPoints = [first, second, .. source.SpawnPoints.Skip(2)] };

        using EditorMapValidationResult overlapResult = EditorMapValidator.Validate(CreateDocument(overlap), null);
        EditorMapValidationStage overlapStage = overlapResult.Report.Stages.Single(
            stage => stage.Category == "Runtime spawn points");
        Assert.False(overlapStage.Success);
        Assert.Contains("overlap", overlapStage.Message, StringComparison.OrdinalIgnoreCase);

        GameMap outsideSafeZone = source with { SafeZone = source.SafeZone with { Radius = 1.0f } };
        using EditorMapValidationResult safeZoneResult = EditorMapValidator.Validate(CreateDocument(outsideSafeZone), null);
        EditorMapValidationStage safeZoneStage = safeZoneResult.Report.Stages.Single(
            stage => stage.Category == "Runtime spawn points");
        Assert.False(safeZoneStage.Success);
        Assert.Contains("initial safe zone", safeZoneStage.Message);
    }

    [Fact]
    public void ProjectValidationBuildsAndCleansIsolatedAudienceOutputs()
    {
        string parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            LoadedRoyaleProject project = RoyaleProjectFactory.Create(parent, "validation-test", "Validation Test");
            EditorProjectSession session = EditorProjectSession.Load(project.Paths.Root);
            EditorMapValidationResult result = EditorMapValidator.Validate(session.Document, session);
            string temporaryRoot = Directory.GetParent(result.ClientAssetRoot)!.FullName;

            Assert.True(result.Report.Success, JoinFailures(result.Report));
            Assert.NotEqual(project.Paths.GeneratedClient, result.ClientAssetRoot);
            Assert.NotEqual(project.Paths.GeneratedServer, result.ServerAssetRoot);
            Assert.True(File.Exists(Path.Combine(result.ClientAssetRoot, ContentCatalog.ModelAssetManifestFileName)));
            Assert.True(File.Exists(Path.Combine(result.ServerAssetRoot, ContentCatalog.ModelAssetManifestFileName)));
            Assert.Empty(Directory.EnumerateFileSystemEntries(project.Paths.GeneratedClient));
            Assert.Empty(Directory.EnumerateFileSystemEntries(project.Paths.GeneratedServer));

            result.Dispose();
            Assert.False(Directory.Exists(temporaryRoot));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void ReportBecomesStaleAfterDocumentOrAssetRevisionChanges()
    {
        EditorMapDocument document = CreateDocument(MapCatalog.LoadById(ContentCatalog.DefaultMapId));
        using EditorMapValidationResult result = EditorMapValidator.Validate(document, null);
        Assert.True(result.Report.IsCurrent(document.Revision, null));

        document.Execute(new SetMapNameCommand(document.Map.Name, "Changed"));

        Assert.False(result.Report.IsCurrent(document.Revision, null));
        Assert.False(result.Report.IsCurrent(result.Report.DocumentRevision, "changed-assets"));
    }

    private static EditorMapDocument CreateDocument(GameMap map) =>
        new(map, sourcePath: null, sourceFingerprint: null, requiresSaveAs: true);

    private static string JoinFailures(EditorMapValidationReport report) =>
        string.Join(Environment.NewLine, report.Stages.Where(stage => !stage.Success).Select(stage => $"{stage.Category}: {stage.Message}"));
}
