using System.Numerics;
using Royale.Client.Rendering;
using Royale.Content;

namespace Royale.Client.Tests;

public sealed class StaticMeshRenderingTests
{
    [Fact]
    public void MapStaticMeshSceneCreatesOneMeshInstancePerStaticBox()
    {
        GameMap map = MapCatalog.LoadDefault();
        IReadOnlyList<StaticMeshInstance> instances = MapStaticMeshScene.CreateInstances(map);

        Assert.Equal(map.StaticBoxes.Count, instances.Count);
    }

    [Fact]
    public void MapStaticMeshSceneInstancesHaveIndependentTransforms()
    {
        IReadOnlyList<StaticMeshInstance> instances = MapStaticMeshScene.CreateInstances(MapCatalog.LoadDefault());

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
    public void MapStaticMeshSceneDrawListIsDeterministic()
    {
        GameMap map = MapCatalog.LoadDefault();
        IReadOnlyList<StaticMeshInstance> first = MapStaticMeshScene.CreateInstances(map);
        IReadOnlyList<StaticMeshInstance> second = MapStaticMeshScene.CreateInstances(map);

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
        RenderCamera camera = DebugCamera.CreateDefault().ToRenderCamera();
        IReadOnlyList<StaticMeshInstance> instances = MapStaticMeshScene.CreateInstances(MapCatalog.LoadDefault());

        foreach (StaticMeshInstance instance in instances)
        {
            Matrix4x4 matrix = StaticMeshDraw.CreateTransposedWorldViewProjection(instance, camera, 1280, 720);

            AssertFinite(matrix);
        }
    }

    [Fact]
    public void StaticBoxTransformsUsePositionSizeAndEulerRotation()
    {
        var staticBox = new StaticBoxDefinition
        {
            Id = "rotated-box",
            Position = new MapVector3(1.0f, 2.0f, 3.0f),
            Size = new MapVector3(2.0f, 4.0f, 6.0f),
            RotationEuler = new MapVector3(0.0f, 90.0f, 0.0f),
        };

        Matrix4x4 expected =
            Matrix4x4.CreateScale(2.0f, 4.0f, 6.0f) *
            Matrix4x4.CreateFromYawPitchRoll(MathF.PI / 2.0f, 0.0f, 0.0f) *
            Matrix4x4.CreateTranslation(1.0f, 2.0f, 3.0f);

        Assert.Equal(expected, MapStaticMeshScene.CreateTransform(staticBox));
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
