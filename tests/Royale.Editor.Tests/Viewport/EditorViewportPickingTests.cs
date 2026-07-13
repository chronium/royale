using System.Numerics;
using Royale.Editor.Documents;
using Royale.Editor.Viewport;
using Royale.Rendering.Cameras;

namespace Royale.Editor.Tests.Viewport;

public sealed class EditorViewportPickingTests
{
    [Fact]
    public void CenterViewportRayUsesCameraForward()
    {
        var camera = new RenderCamera(Vector3.Zero, 0, 0);
        EditorRay ray = EditorViewportPicking.CreateRay(camera, 400, 300, 800, 600);
        Assert.True(Vector3.Distance(-Vector3.UnitZ, ray.Direction) < 0.0001f);
    }

    [Fact]
    public void PicksNearestOrientedBoundsAndMissClearsResult()
    {
        EditorEntityIdentity nearIdentity = Identity(0);
        EditorEntityIdentity farIdentity = Identity(1);
        EditorRay ray = new(Vector3.Zero, -Vector3.UnitZ);
        EditorPickTarget far = Box(farIdentity, Matrix4x4.CreateScale(2, 1, 1) * Matrix4x4.CreateRotationY(MathF.PI / 4) * Matrix4x4.CreateTranslation(0, 0, -10));
        EditorPickTarget near = Box(nearIdentity, Matrix4x4.CreateRotationY(MathF.PI / 4) * Matrix4x4.CreateTranslation(0, 0, -4));

        EditorPickResult? hit = EditorViewportPicking.Pick(ray, [far, near]);
        Assert.Equal(nearIdentity, hit?.Identity);
        Assert.Null(EditorViewportPicking.Pick(new EditorRay(Vector3.Zero, Vector3.UnitY), [far, near]));
    }

    [Fact]
    public void MarkerProxyAndGizmoExclusionUseSameNearestHitPath()
    {
        EditorEntityIdentity identity = new(Guid.NewGuid(), EditorEntityKind.LootPoint, 0);
        float radius = EditorViewportPicking.LootProxyRadius;
        var marker = new EditorPickTarget(
            identity,
            Matrix4x4.CreateTranslation(0, 0, -3),
            new Vector3(-radius),
            new Vector3(radius));
        EditorRay ray = new(Vector3.Zero, -Vector3.UnitZ);

        Assert.Equal(identity, EditorViewportPicking.Pick(ray, [marker])?.Identity);
        Assert.Null(EditorViewportPicking.Pick(ray, [marker], gizmoOwnsPointer: true));
    }

    private static EditorPickTarget Box(EditorEntityIdentity identity, Matrix4x4 transform) =>
        new(identity, transform, new Vector3(-0.5f), new Vector3(0.5f));

    private static EditorEntityIdentity Identity(int index) =>
        new(Guid.NewGuid(), EditorEntityKind.StaticBox, index);
}
