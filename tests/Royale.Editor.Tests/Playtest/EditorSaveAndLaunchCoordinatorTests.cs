using Royale.Content;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Playtest;
using Royale.Editor.Tests.Infrastructure;

namespace Royale.Editor.Tests.Playtest;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class EditorSaveAndLaunchCoordinatorTests
{
    [Fact]
    public void StandaloneSaveAsContinuesOnlyAfterSuccessfulDestinationSave()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            EditorMapDocument document = CreateDocument();
            using var coordinator = new EditorSaveAndLaunchCoordinator(() => "/tmp/repository");

            EditorSaveAndLaunchOutcome pending = coordinator.Begin(document, projectSession: null);

            Assert.Equal(EditorSaveAndLaunchStatus.AwaitingSaveAs, pending.Status);
            Assert.True(coordinator.AwaitingSaveAs);
            Assert.Null(pending.Request);

            string destination = Path.Combine(directory, document.Map.Id + ".json");
            EditorSaveAndLaunchOutcome ready = coordinator.CompleteSaveAs(destination);

            Assert.Equal(EditorSaveAndLaunchStatus.Ready, ready.Status);
            Assert.False(coordinator.AwaitingSaveAs);
            Assert.Equal(Path.GetFullPath(destination), ready.Request!.MapFile);
            Assert.True(File.Exists(destination));
            Assert.False(document.RequiresSaveAs);
            ready.Request.Artifacts?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void StandaloneSaveAsCancellationAbortsPendingLaunch()
    {
        using var coordinator = new EditorSaveAndLaunchCoordinator(() => "/tmp/repository");
        EditorSaveAndLaunchOutcome pending = coordinator.Begin(CreateDocument(), projectSession: null);
        Assert.Equal(EditorSaveAndLaunchStatus.AwaitingSaveAs, pending.Status);

        EditorSaveAndLaunchOutcome cancelled = coordinator.CompleteSaveAs(path: null);

        Assert.Equal(EditorSaveAndLaunchStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.Request);
        Assert.False(coordinator.AwaitingSaveAs);
    }

    [Fact]
    public void ValidationFailureNeverSavesOrCreatesLaunchRequest()
    {
        GameMap source = MapCatalog.LoadById(ContentCatalog.DefaultMapId);
        GameMap invalid = source with { SafeZone = source.SafeZone with { Radius = 1.0f } };
        var document = new EditorMapDocument(invalid, null, null, requiresSaveAs: true);
        using var coordinator = new EditorSaveAndLaunchCoordinator(() => "/tmp/repository");

        EditorSaveAndLaunchOutcome outcome = coordinator.Begin(document, projectSession: null);

        Assert.Equal(EditorSaveAndLaunchStatus.ValidationFailed, outcome.Status);
        Assert.False(outcome.Report.Success);
        Assert.Null(outcome.Request);
        Assert.False(coordinator.AwaitingSaveAs);
    }

    private static EditorMapDocument CreateDocument() =>
        new(
            MapCatalog.LoadById(ContentCatalog.DefaultMapId),
            sourcePath: null,
            sourceFingerprint: null,
            requiresSaveAs: true);
}
