using System.Numerics;
using Royale.Client.Rendering;

namespace Royale.Client.Tests;

public sealed class StaticMeshRenderingTests
{
    [Fact]
    public void GrayBoxPreviewSceneCreatesMultipleMeshInstances()
    {
        IReadOnlyList<StaticMeshInstance> instances = GrayBoxPreviewScene.CreateInstances();

        Assert.True(instances.Count > 1);
    }

    [Fact]
    public void GrayBoxPreviewSceneInstancesHaveIndependentTransforms()
    {
        IReadOnlyList<StaticMeshInstance> instances = GrayBoxPreviewScene.CreateInstances();

        for (int left = 0; left < instances.Count; left++)
        {
            for (int right = left + 1; right < instances.Count; right++)
                Assert.NotEqual(instances[left].Transform, instances[right].Transform);
        }
    }

    [Fact]
    public void UnitBoxMeshCreatesValidIndexedGeometry()
    {
        StaticMeshGeometry mesh = UnitBoxMesh.Create();

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);

        foreach (ushort index in mesh.Indices)
            Assert.InRange(index, 0, mesh.Vertices.Count - 1);
    }

    [Fact]
    public void GrayBoxPreviewSceneDrawListIsDeterministic()
    {
        IReadOnlyList<StaticMeshInstance> first = GrayBoxPreviewScene.CreateInstances();
        IReadOnlyList<StaticMeshInstance> second = GrayBoxPreviewScene.CreateInstances();

        Assert.Equal(first.Count, second.Count);

        for (int index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].DebugName, second[index].DebugName);
            Assert.Equal(first[index].Transform, second[index].Transform);
        }
    }

    [Fact]
    public void WorldViewProjectionCanBeCreatedForEveryPreviewInstance()
    {
        DebugCamera camera = DebugCamera.CreateDefault();
        IReadOnlyList<StaticMeshInstance> instances = GrayBoxPreviewScene.CreateInstances();

        foreach (StaticMeshInstance instance in instances)
        {
            Matrix4x4 matrix = StaticMeshDraw.CreateTransposedWorldViewProjection(instance, camera, 1280, 720);

            AssertFinite(matrix);
        }
    }

    private static void AssertFinite(Matrix4x4 matrix)
    {
        Assert.True(float.IsFinite(matrix.M11));
        Assert.True(float.IsFinite(matrix.M12));
        Assert.True(float.IsFinite(matrix.M13));
        Assert.True(float.IsFinite(matrix.M14));
        Assert.True(float.IsFinite(matrix.M21));
        Assert.True(float.IsFinite(matrix.M22));
        Assert.True(float.IsFinite(matrix.M23));
        Assert.True(float.IsFinite(matrix.M24));
        Assert.True(float.IsFinite(matrix.M31));
        Assert.True(float.IsFinite(matrix.M32));
        Assert.True(float.IsFinite(matrix.M33));
        Assert.True(float.IsFinite(matrix.M34));
        Assert.True(float.IsFinite(matrix.M41));
        Assert.True(float.IsFinite(matrix.M42));
        Assert.True(float.IsFinite(matrix.M43));
        Assert.True(float.IsFinite(matrix.M44));
    }
}
