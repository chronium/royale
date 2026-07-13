using System.Numerics;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Viewport;
using Royale.Editor.Workspace;

namespace Royale.Editor.Tests.Documents;

public sealed class EditorEntityTransformTests
{
    [Theory]
    [InlineData(EditorEntityKind.StaticBox, EditorTransformCapabilities.Translate | EditorTransformCapabilities.Rotate | EditorTransformCapabilities.Scale)]
    [InlineData(EditorEntityKind.StaticModel, EditorTransformCapabilities.Translate | EditorTransformCapabilities.Rotate | EditorTransformCapabilities.Scale)]
    [InlineData(EditorEntityKind.SpawnPoint, EditorTransformCapabilities.Translate | EditorTransformCapabilities.Rotate)]
    [InlineData(EditorEntityKind.LootPoint, EditorTransformCapabilities.Translate)]
    [InlineData(EditorEntityKind.NavigationWaypoint, EditorTransformCapabilities.Translate)]
    [InlineData(EditorEntityKind.NavigationLink, EditorTransformCapabilities.None)]
    public void RoutesCapabilitiesByEntityKind(EditorEntityKind kind, EditorTransformCapabilities expected) =>
        Assert.Equal(expected, EditorEntityTransforms.GetCapabilities(kind));

    [Fact]
    public void CommandResolvesStableIdentityAndRestoresCompleteTransform()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity identity = document.Identities.Single(value => value.Kind == EditorEntityKind.StaticBox);
        EditorEntityTransform before = EditorEntityTransforms.Get(document, identity);
        EditorEntityTransform after = new(new Vector3(4, 5, 6), new Vector3(10, 20, 30), new Vector3(7, 8, 9));

        document.Execute(new SetEntityTransformCommand(identity.EditorId, before, after));
        Assert.Equal(after, EditorEntityTransforms.Get(document, identity));
        Assert.True(document.Undo());
        Assert.Equal(before, EditorEntityTransforms.Get(document, identity));
        Assert.True(document.Redo());
        Assert.Equal(after, EditorEntityTransforms.Get(document, identity));
    }

    [Fact]
    public void SelectionStaysOnIdentityAcrossTransformUndoRedoAndPresentationRebuilds()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity identity = document.Identities.Single(value => value.Kind == EditorEntityKind.SpawnPoint);
        var selection = new EditorSelectionState();
        selection.Select(document, identity.EditorId);
        EditorEntityTransform before = EditorEntityTransforms.Get(document, identity);
        EditorEntityTransform after = before with { Position = new Vector3(12, 0, 2) };

        document.Execute(new SetEntityTransformCommand(identity.EditorId, before, after));
        document.Undo();
        document.Redo();

        Assert.Equal(identity, selection.Resolve(document));
        Assert.Equal(identity.EditorId, selection.SelectedEditorId);
    }

    [Fact]
    public void ManipulationPreviewsWithoutHistoryAndCommitsExactlyOneEntry()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity identity = document.Identities.Single(value => value.Kind == EditorEntityKind.StaticBox);
        EditorEntityTransform before = EditorEntityTransforms.Get(document, identity);
        var manipulation = new EditorTransformManipulation();

        manipulation.Begin(document, identity);
        manipulation.Preview(document, before with { Position = new Vector3(2, 3, 4) });
        manipulation.Preview(document, before with { Position = new Vector3(5, 6, 7) });
        Assert.False(document.CanUndo);
        Assert.True(manipulation.Complete(document, out string? error));

        Assert.Null(error);
        Assert.True(document.CanUndo);
        document.Undo();
        Assert.Equal(before, EditorEntityTransforms.Get(document, identity));
        Assert.False(document.CanUndo);
    }

    [Fact]
    public void ManipulationCancelAndNoOpDoNotAddHistory()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity identity = document.Identities.Single(value => value.Kind == EditorEntityKind.SpawnPoint);
        EditorEntityTransform before = EditorEntityTransforms.Get(document, identity);
        var manipulation = new EditorTransformManipulation();

        manipulation.Begin(document, identity);
        manipulation.Preview(document, before with { Position = Vector3.One * 8 });
        Assert.True(manipulation.Cancel(document));
        Assert.Equal(before, EditorEntityTransforms.Get(document, identity));
        Assert.False(document.CanUndo);

        manipulation.Begin(document, identity);
        manipulation.Preview(document, before);
        Assert.False(manipulation.Complete(document, out string? error));
        Assert.Null(error);
        Assert.False(document.CanUndo);
    }

    [Theory]
    [InlineData(EditorEntityKind.StaticBox)]
    [InlineData(EditorEntityKind.StaticModel)]
    public void InvalidCompletedScaleIsRejectedAndOriginalRestored(EditorEntityKind kind)
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity identity = document.Identities.Single(value => value.Kind == kind);
        EditorEntityTransform before = EditorEntityTransforms.Get(document, identity);
        var manipulation = new EditorTransformManipulation();

        manipulation.Begin(document, identity);
        manipulation.Preview(document, before with { ScaleOrSize = new Vector3(1, 0, 1) });

        Assert.False(manipulation.Complete(document, out string? error));
        Assert.NotNull(error);
        Assert.Equal(before, EditorEntityTransforms.Get(document, identity));
        Assert.False(document.CanUndo);
    }

    [Fact]
    public void MatrixConversionAndTransformDecompositionRoundTrip()
    {
        var transform = new EditorEntityTransform(
            new Vector3(4, -2, 7),
            new Vector3(12, -35, 18),
            new Vector3(2, 3, 4));
        Matrix4x4 matrix = transform.CreateMatrix();
        Matrix4x4 converted = EditorMatrixConverter.FromImGuizmo(EditorMatrixConverter.ToImGuizmo(matrix));
        EditorEntityTransform decomposed = EditorEntityTransform.FromMatrix(converted);

        AssertMatrixClose(matrix, decomposed.CreateMatrix());
    }

    private static EditorMapDocument CreateDocument() => new(new GameMap
    {
        Id = "test",
        StaticBoxes = [new StaticBoxDefinition { Id = "box", Size = new MapVector3(2, 3, 4) }],
        StaticModels = [new StaticModelDefinition { Id = "model", AssetId = "asset", Scale = new MapVector3(1, 1, 1) }],
        SpawnPoints = [new MapSpawnPoint { Id = "spawn" }],
        LootPoints = [new MapLootPoint { Id = "loot" }],
        Navigation = new MapNavigationDefinition
        {
            Waypoints = [new MapNavigationWaypoint { Id = "node" }],
            Links = [new MapNavigationLink { From = "node", To = "node" }],
        },
    }, null, null, false);

    private static void AssertMatrixClose(Matrix4x4 expected, Matrix4x4 actual)
    {
        float[] left = [expected.M11, expected.M12, expected.M13, expected.M14, expected.M21, expected.M22, expected.M23, expected.M24, expected.M31, expected.M32, expected.M33, expected.M34, expected.M41, expected.M42, expected.M43, expected.M44];
        float[] right = [actual.M11, actual.M12, actual.M13, actual.M14, actual.M21, actual.M22, actual.M23, actual.M24, actual.M31, actual.M32, actual.M33, actual.M34, actual.M41, actual.M42, actual.M43, actual.M44];
        for (int index = 0; index < left.Length; index++)
            Assert.InRange(right[index], left[index] - 0.0001f, left[index] + 0.0001f);
    }
}
