using System.Numerics;
using Royale.Rendering.Meshes;

namespace Royale.Rendering.Tests.Rendering.Meshes;

public sealed class ModelThumbnailFramingTests
{
    [Theory]
    [MemberData(nameof(BoundsCases))]
    public void BoundsIncludeOffsetTallWideAndFlatGeometry(Vector3[] positions, Vector3 expectedMinimum, Vector3 expectedMaximum)
    {
        ModelBounds bounds = ModelThumbnailFraming.CalculateBounds(Asset(positions));

        AssertVector(expectedMinimum, bounds.Minimum);
        AssertVector(expectedMaximum, bounds.Maximum);
    }

    [Fact]
    public void DegenerateGeometryUsesSafeFiniteFraming()
    {
        ModelBounds bounds = ModelThumbnailFraming.CalculateBounds(Asset([new Vector3(3, 4, 5)]));
        var camera = ModelThumbnailFraming.CreateCamera(bounds);

        Assert.True(bounds.HalfSize.X > 0 && bounds.HalfSize.Y > 0 && bounds.HalfSize.Z > 0);
        Assert.True(float.IsFinite(camera.Position.X));
        Assert.True(camera.NearPlane > 0);
        Assert.True(camera.FarPlane > camera.NearPlane);
        Assert.InRange(Vector3.Dot(camera.Forward, Vector3.Normalize(bounds.Center - camera.Position)), 0.999f, 1.001f);
    }

    [Fact]
    public void SceneUsesIdentityInstancesAndEveryPrimitive()
    {
        StaticMeshAsset asset = Asset([Vector3.Zero, Vector3.One], primitiveCount: 2);
        StaticMeshScene scene = ModelThumbnailFraming.CreateScene(asset);

        Assert.Equal(2, scene.ModelAssetBatches.Count);
        Assert.All(scene.ModelAssetBatches, batch => Assert.Equal(Matrix4x4.Identity, Assert.Single(batch.Instances).Transform));
        Assert.Empty(scene.UnitBoxInstances);
    }

    [Fact]
    public void CameraUsesElevatedNegativeDiagonalToShowModelFronts()
    {
        ModelBounds bounds = new(new Vector3(-1), new Vector3(1));
        var camera = ModelThumbnailFraming.CreateCamera(bounds);
        Vector3 offset = camera.Position - bounds.Center;

        Assert.True(offset.X < 0);
        Assert.True(offset.Y > 0);
        Assert.True(offset.Z < 0);
        Assert.Equal(MathF.Abs(offset.X), MathF.Abs(offset.Z), 5);
    }

    public static TheoryData<Vector3[], Vector3, Vector3> BoundsCases => new()
    {
        { [new Vector3(5, 2, -3), new Vector3(7, 4, -1)], new Vector3(5, 2, -3), new Vector3(7, 4, -1) },
        { [new Vector3(-1, -5, -1), new Vector3(1, 5, 1)], new Vector3(-1, -5, -1), new Vector3(1, 5, 1) },
        { [new Vector3(-6, -1, -1), new Vector3(6, 1, 1)], new Vector3(-6, -1, -1), new Vector3(6, 1, 1) },
        { [new Vector3(-2, 0, -3), new Vector3(2, 0, 3)], new Vector3(-2, -0.025f, -3), new Vector3(2, 0.025f, 3) },
    };

    private static StaticMeshAsset Asset(Vector3[] positions, int primitiveCount = 1)
    {
        StaticMeshGeometry geometry = new(positions.Select(position => new StaticMeshVertex(position, Vector3.UnitY)).ToArray(), []);
        return new StaticMeshAsset("test", Enumerable.Range(0, primitiveCount)
            .Select(index => new StaticMeshPrimitive(index.ToString(), geometry, StaticMeshMaterial.GrayBox))
            .ToArray());
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 5);
        Assert.Equal(expected.Y, actual.Y, 5);
        Assert.Equal(expected.Z, actual.Z, 5);
    }
}
