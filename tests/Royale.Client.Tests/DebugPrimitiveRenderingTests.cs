using System.Numerics;
using System.Runtime.InteropServices;
using Royale.Client.Gameplay;
using Royale.Client.Rendering;
using Royale.Content;

namespace Royale.Client.Tests;

public sealed class DebugPrimitiveRenderingTests
{
    [Fact]
    public void EmptyDebugPrimitiveListProducesNoVertices()
    {
        var primitives = new DebugPrimitiveList();

        Assert.True(primitives.IsEmpty);
        Assert.Empty(primitives.ToVertices());
    }

    [Fact]
    public void DebugLineBuilderEmitsFiniteVertices()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddLine(Vector3.Zero, Vector3.One, new Vector4(1.0f, 0.5f, 0.25f, 1.0f));

        DebugLineVertex[] vertices = primitives.ToVertices();

        Assert.Equal(2, vertices.Length);
        foreach (DebugLineVertex vertex in vertices)
        {
            AssertFinite(vertex.Position);
            AssertFinite(vertex.Color);
        }
    }

    [Fact]
    public void WireBoxEmitsTwelveLines()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddWireBox(new Vector3(1.0f, 2.0f, 3.0f), Matrix4x4.Identity, Vector4.One);

        Assert.Equal(12, primitives.LineCount);
        Assert.Equal(24, primitives.ToVertices().Length);
    }

    [Fact]
    public void CapsuleHelperEmitsExpectedLineCount()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddCapsule(Vector3.UnitY * 0.35f, Vector3.UnitY * 1.45f, 0.35f, Vector4.One);

        Assert.Equal(52, primitives.LineCount);
    }

    [Fact]
    public void CircleHelperEmitsRequestedLineCount()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddCircleXz(Vector3.Zero, 10.0f, Vector4.One, segmentCount: 48);

        Assert.Equal(48, primitives.LineCount);
    }

    [Fact]
    public void DebugLineVertexLayoutMatchesPositionAndColor()
    {
        Assert.Equal(Marshal.SizeOf<Vector3>() + Marshal.SizeOf<Vector4>(), DebugLineVertex.Stride);
        Assert.Equal(0u, DebugLineVertex.PositionOffset);
        Assert.Equal(Marshal.SizeOf<Vector3>(), DebugLineVertex.ColorOffset);
        Assert.Equal(DebugLineVertex.Stride, Marshal.SizeOf<DebugLineVertex>());
    }

    private static void AssertFinite(Vector3 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
    }

    private static void AssertFinite(Vector4 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
        Assert.True(float.IsFinite(vector.W));
    }
}

[Collection(Box3DNativeTestCollection.Name)]
public sealed class DebugPrimitiveNativeRenderingTests
{
    [Fact]
    public void DebugSceneBuilderEmitsBox3DAndGameplayLinesForDefaultMap()
    {
        GameMap map = MapCatalog.LoadDefault();
        using LocalPlayerController localPlayer = LocalPlayerController.Create(map);

        DebugPrimitiveList primitives = DebugSceneBuilder.Build(map, localPlayer);

        Assert.False(primitives.IsEmpty);
        Assert.True(primitives.LineCount >= map.StaticBoxes.Count * 12);
        Assert.NotEmpty(primitives.ToVertices());
    }
}
