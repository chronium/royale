using System.Numerics;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Viewport;
using Royale.Editor.Viewport.FaceSnap;
using Royale.Editor.Projects;
using Royale.Simulation.World;

using Royale.Editor.Tests.Infrastructure;

namespace Royale.Editor.Tests.Viewport;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class EditorFaceSnapSessionTests
{
    [Fact]
    public void PreviewMissRestorationAndCancelDoNotAddHistory()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity selected = document.Identities[0];
        EditorEntityTransform original = EditorEntityTransforms.Get(document, selected);
        using var session = CreateSession(document, selected);

        Assert.True(session.TryPreview(new EditorRay(Vector3.Zero, -Vector3.UnitZ), new EditorFaceSnapSettings()));
        Assert.Equal("target", session.Hit?.Collider.ContentId);
        Assert.False(document.CanUndo);
        Assert.NotEqual(original, EditorEntityTransforms.Get(document, selected));

        Assert.False(session.TryPreview(new EditorRay(Vector3.Zero, Vector3.UnitY), new EditorFaceSnapSettings()));
        Assert.Equal(original, EditorEntityTransforms.Get(document, selected));
        Assert.False(document.CanUndo);

        Assert.True(session.TryPreview(new EditorRay(Vector3.Zero, -Vector3.UnitZ), new EditorFaceSnapSettings()));
        session.Cancel();
        Assert.Equal(original, EditorEntityTransforms.Get(document, selected));
        Assert.False(document.CanUndo);
    }

    [Fact]
    public void CommitCreatesOneCommandWithUndoRedoAndExcludesSelectedCollider()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity selected = document.Identities[0];
        EditorEntityTransform original = EditorEntityTransforms.Get(document, selected);
        using var session = CreateSession(document, selected);

        Assert.True(session.TryPreview(new EditorRay(Vector3.Zero, -Vector3.UnitZ), new EditorFaceSnapSettings()));
        Assert.Equal("target", session.Hit?.Collider.ContentId);
        EditorEntityTransform preview = EditorEntityTransforms.Get(document, selected);
        Assert.InRange(preview.Position.Z, -4.001f, -3.999f);
        Assert.True(session.Commit());

        Assert.True(document.CanUndo);
        Assert.True(document.Undo());
        Assert.Equal(original, EditorEntityTransforms.Get(document, selected));
        Assert.False(document.CanUndo);
        Assert.True(document.Redo());
        Assert.True(preview.NearlyEquals(EditorEntityTransforms.Get(document, selected)));
    }

    [Fact]
    public void GizmoSessionExcludesSelectedColliderAndRestoresCandidateOnMiss()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity selected = document.Identities[0];
        EditorEntityTransform original = EditorEntityTransforms.Get(document, selected);
        var bounds = new EditorPickTarget(
            selected,
            original.CreateMatrix(),
            new Vector3(-0.5f),
            new Vector3(0.5f));
        using var session = new EditorGizmoFaceSnapSession(
            document,
            selected,
            bounds,
            MapStaticCollisionWorld.Create(document.Map));
        EditorEntityTransform candidate = original with { Position = new Vector3(0, 0, -3) };
        session.UpdateCandidate(candidate);

        Assert.True(session.TrySnap(
            new EditorRay(Vector3.Zero, -Vector3.UnitZ),
            EditorTranslationConstraint.Z,
            EditorTransformOrientation.World,
            out EditorEntityTransform snapped));
        Assert.Equal("target", session.Hit?.Collider.ContentId);
        Assert.InRange(snapped.Position.Z, -4.0001f, -3.9999f);
        Assert.False(document.CanUndo);

        Assert.False(session.TrySnap(
            new EditorRay(Vector3.Zero, Vector3.UnitY),
            EditorTranslationConstraint.Z,
            EditorTransformOrientation.World,
            out EditorEntityTransform missed));
        Assert.Equal(candidate, missed);
        Assert.Null(session.Hit);
    }

    [Fact]
    public void GizmoSnapCompletesAsOneUndoableManipulation()
    {
        EditorMapDocument document = CreateDocument();
        EditorEntityIdentity selected = document.Identities[0];
        EditorEntityTransform original = EditorEntityTransforms.Get(document, selected);
        var bounds = new EditorPickTarget(
            selected,
            original.CreateMatrix(),
            new Vector3(-0.5f),
            new Vector3(0.5f));
        using var session = new EditorGizmoFaceSnapSession(
            document,
            selected,
            bounds,
            MapStaticCollisionWorld.Create(document.Map));
        var manipulation = new EditorTransformManipulation();
        manipulation.Begin(document, selected);
        session.UpdateCandidate(original with { Position = new Vector3(0, 0, -3) });

        Assert.True(session.TrySnap(
            new EditorRay(Vector3.Zero, -Vector3.UnitZ),
            EditorTranslationConstraint.Z,
            EditorTransformOrientation.World,
            out EditorEntityTransform snapped));
        manipulation.Preview(document, snapped);
        Assert.True(manipulation.Complete(document, out string? error));
        Assert.Null(error);

        Assert.True(document.Undo());
        Assert.Equal(original, EditorEntityTransforms.Get(document, selected));
        Assert.False(document.CanUndo);
        Assert.True(document.Redo());
        Assert.True(snapped.NearlyEquals(EditorEntityTransforms.Get(document, selected)));
    }

    [Fact]
    public void ProjectSessionRebuildsServerCollisionOutputBeforeCreatingWorld()
    {
        string parent = Path.Combine(Path.GetTempPath(), "royale-face-snap-project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            LoadedRoyaleProject project = RoyaleProjectFactory.Create(parent, "snap-project", "Snap Project");
            EditorProjectSession projectSession = EditorProjectSession.Load(project.Paths.Root);

            using MapStaticCollisionWorld world = EditorFaceSnapCollisionWorldFactory.Create(
                projectSession.Document,
                projectSession);

            Assert.True(File.Exists(Path.Combine(
                project.Paths.GeneratedServer,
                "model-assets.json")));
            Assert.Equal(projectSession.Document.Map.StaticBoxes.Count, world.StaticBoxColliderCount);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private static EditorFaceSnapSession CreateSession(
        EditorMapDocument document,
        EditorEntityIdentity selected)
    {
        var bounds = new EditorPickTarget(
            selected,
            EditorEntityTransforms.Get(document, selected).CreateMatrix(),
            new Vector3(-0.5f),
            new Vector3(0.5f));
        return new EditorFaceSnapSession(
            document,
            selected,
            bounds,
            MapStaticCollisionWorld.Create(document.Map));
    }

    private static EditorMapDocument CreateDocument() => new(new GameMap
    {
        Id = "face-snap-session",
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "selected",
                Position = new MapVector3(0.0f, 0.0f, -2.0f),
                Size = new MapVector3(2.0f, 2.0f, 2.0f),
            },
            new StaticBoxDefinition
            {
                Id = "target",
                Position = new MapVector3(0.0f, 0.0f, -6.0f),
                Size = new MapVector3(6.0f, 6.0f, 2.0f),
            },
        ],
    }, null, null, false);
}
