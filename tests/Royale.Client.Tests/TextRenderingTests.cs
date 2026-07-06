using System.Numerics;
using System.Runtime.InteropServices;
using BlurgText;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;

namespace Royale.Client.Tests;

public sealed class TextRenderingTests
{
    [Fact]
    public void TextVertexLayoutMatchesScreenTextShader()
    {
        Assert.Equal(Marshal.SizeOf<Vector2>() * 2 + Marshal.SizeOf<Vector4>(), TextVertex.Stride);
        Assert.Equal(0u, TextVertex.PositionOffset);
        Assert.Equal(Marshal.SizeOf<Vector2>(), TextVertex.TexCoordOffset);
        Assert.Equal(Marshal.SizeOf<Vector2>() * 2, TextVertex.ColorOffset);
        Assert.Equal(TextVertex.Stride, Marshal.SizeOf<TextVertex>());
    }

    [Theory]
    [InlineData(0.0f, 0.0f, -1.0f, 1.0f)]
    [InlineData(960.0f, 540.0f, 0.0f, 0.0f)]
    [InlineData(1920.0f, 1080.0f, 1.0f, -1.0f)]
    public void PixelPositionsMapToClipSpace(float x, float y, float expectedX, float expectedY)
    {
        Vector2 clip = TextPixelTransform.ToClipSpace(new Vector2(x, y), 1920, 1080);

        Assert.Equal(expectedX, clip.X, precision: 5);
        Assert.Equal(expectedY, clip.Y, precision: 5);
    }

    [Fact]
    public void QuadBuilderRoundsPositionsAndPreservesUvsTintAndIndices()
    {
        var source = new TextQuadSource(
            new IntPtr(42),
            3,
            4,
            10,
            12,
            0.1f,
            0.2f,
            0.3f,
            0.4f,
            new BlurgColor(255, 128, 64, 32));

        TextQuadBatch batch = TextQuadBatchBuilder.Create([source], new Vector2(10.4f, 20.4f));

        Assert.Equal(4, batch.Vertices.Count);
        Assert.Equal(6, batch.Indices.Count);
        Assert.Equal([0, 1, 2, 2, 1, 3], batch.Indices);
        Assert.Equal(new Vector2(13.0f, 24.0f), batch.Vertices[0].Position);
        Assert.Equal(new Vector2(23.0f, 24.0f), batch.Vertices[1].Position);
        Assert.Equal(new Vector2(13.0f, 36.0f), batch.Vertices[2].Position);
        Assert.Equal(new Vector2(23.0f, 36.0f), batch.Vertices[3].Position);
        Assert.Equal(new Vector2(0.1f, 0.2f), batch.Vertices[0].TexCoord);
        Assert.Equal(new Vector2(0.3f, 0.4f), batch.Vertices[3].TexCoord);
        Assert.Equal(new Vector4(1.0f, 128.0f / 255.0f, 64.0f / 255.0f, 32.0f / 255.0f), batch.Vertices[0].Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void EmptyOrWhitespaceTextDoesNotCreateDrawableQuads(string text)
    {
        TextQuadBatch batch = TextQuadBatchBuilder.CreateForText(
            text,
            [new TextQuadSource(new IntPtr(1), 0, 0, 8, 8, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White)],
            Vector2.Zero);

        Assert.True(batch.IsEmpty);
        Assert.Empty(batch.Vertices);
        Assert.Empty(batch.Indices);
        Assert.Empty(batch.DrawCommands);
    }

    [Fact]
    public void QuadBuilderCreatesOneDrawCommandPerTextureRun()
    {
        TextQuadBatch batch = TextQuadBatchBuilder.Create(
            [
                new TextQuadSource(new IntPtr(1), 0, 0, 8, 8, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White),
                new TextQuadSource(new IntPtr(1), 8, 0, 8, 8, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White),
                new TextQuadSource(new IntPtr(2), 16, 0, 8, 8, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White),
            ],
            Vector2.Zero);

        Assert.Equal(2, batch.DrawCommands.Count);
        Assert.Equal(new TextDrawCommand(new IntPtr(1), 0, 12), batch.DrawCommands[0]);
        Assert.Equal(new TextDrawCommand(new IntPtr(2), 12, 6), batch.DrawCommands[1]);
    }

    [Fact]
    public void ProjectedQuadBuilderRoundsCornersAndGroupsTextureRuns()
    {
        TextQuadBatch batch = TextQuadBatchBuilder.CreateProjected(
            [
                new TextProjectedQuadSource(
                    new IntPtr(1),
                    new Vector2(1.25f, 2.5f),
                    new Vector2(9.75f, 3.5f),
                    new Vector2(2.25f, 12.5f),
                    new Vector2(10.75f, 13.5f),
                    0.0f,
                    0.0f,
                    0.5f,
                    0.5f,
                    BlurgColor.White),
                new TextProjectedQuadSource(
                    new IntPtr(1),
                    new Vector2(20.0f, 2.0f),
                    new Vector2(28.0f, 2.0f),
                    new Vector2(20.0f, 10.0f),
                    new Vector2(28.0f, 10.0f),
                    0.0f,
                    0.0f,
                    0.5f,
                    0.5f,
                    BlurgColor.White),
                new TextProjectedQuadSource(
                    new IntPtr(2),
                    new Vector2(40.0f, 2.0f),
                    new Vector2(48.0f, 2.0f),
                    new Vector2(40.0f, 10.0f),
                    new Vector2(48.0f, 10.0f),
                    0.0f,
                    0.0f,
                    0.5f,
                    0.5f,
                    BlurgColor.White),
            ]);

        Assert.Equal(12, batch.Vertices.Count);
        Assert.Equal(new Vector2(1.0f, 2.0f), batch.Vertices[0].Position);
        Assert.Equal(new Vector2(11.0f, 14.0f), batch.Vertices[3].Position);
        Assert.Equal([0, 1, 2, 2, 1, 3], batch.Indices.Take(6).ToArray());
        Assert.Equal(2, batch.DrawCommands.Count);
        Assert.Equal(new TextDrawCommand(new IntPtr(1), 0, 12), batch.DrawCommands[0]);
        Assert.Equal(new TextDrawCommand(new IntPtr(2), 12, 6), batch.DrawCommands[1]);
    }

    [Fact]
    public void WorldPointProjectionMapsCameraForwardToScreenCenter()
    {
        var camera = new RenderCamera(Vector3.Zero, 0.0f, 0.0f);

        bool projected = WorldTextProjector.TryProjectPoint(new Vector3(0.0f, 0.0f, -5.0f), camera, 1920, 1080, out Vector2 screen);

        Assert.True(projected);
        Assert.Equal(960.0f, screen.X, precision: 3);
        Assert.Equal(540.0f, screen.Y, precision: 3);
    }

    [Fact]
    public void CameraFacingBasisUsesRenderCameraRightAndUp()
    {
        var camera = new RenderCamera(Vector3.Zero, 0.0f, 0.0f);

        WorldTextBasis basis = WorldTextProjector.CreateCameraFacingBasis(camera);

        AssertVector(Vector3.UnitX, basis.Right);
        AssertVector(Vector3.UnitY, basis.Up);
    }

    [Fact]
    public void FixedFacingBasisPreservesAuthoredDirections()
    {
        WorldTextBillboard billboard = WorldTextBillboard.FixedFacing(
            "Fixed",
            new Vector3(0.0f, 0.0f, -5.0f),
            1.0f,
            Vector2.Zero,
            new WorldTextBasis(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(0.0f, 3.0f, 0.0f)),
            BlurgColor.White,
            new BlurgColor(0, 0, 0, 180),
            Vector2.One);

        bool resolved = WorldTextProjector.TryResolveBasis(billboard, new RenderCamera(Vector3.Zero, 0.0f, 0.0f), out WorldTextBasis basis);

        Assert.True(resolved);
        AssertVector(Vector3.UnitX, basis.Right);
        AssertVector(Vector3.UnitY, basis.Up);
    }

    [Fact]
    public void WorldHeightScalesProjectedTextQuads()
    {
        var camera = new RenderCamera(Vector3.Zero, 0.0f, 0.0f);
        TextQuadSource glyph = new(new IntPtr(1), 0, 0, 100, 50, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White);

        WorldTextBillboard small = FixedLabel(worldHeight: 1.0f);
        WorldTextBillboard large = FixedLabel(worldHeight: 2.0f);

        TextProjectedQuadSource smallQuad = Assert.Single(WorldTextProjector.CreateProjectedQuads(small, [glyph], new Vector2(100.0f, 50.0f), camera, 1920, 1080));
        TextProjectedQuadSource largeQuad = Assert.Single(WorldTextProjector.CreateProjectedQuads(large, [glyph], new Vector2(100.0f, 50.0f), camera, 1920, 1080));

        float smallHeight = Vector2.Distance(smallQuad.TopLeft, smallQuad.BottomLeft);
        float largeHeight = Vector2.Distance(largeQuad.TopLeft, largeQuad.BottomLeft);
        Assert.InRange(largeHeight / smallHeight, 1.99f, 2.01f);
    }

    [Fact]
    public void ProjectedWorldTextScreenOffsetDoesNotChangeProjectedSize()
    {
        var camera = new RenderCamera(Vector3.Zero, 0.0f, 0.0f);
        TextQuadSource glyph = new(new IntPtr(1), 0, 0, 100, 50, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White);
        WorldTextBillboard billboard = FixedLabel(worldHeight: 1.0f);

        TextProjectedQuadSource baseQuad = Assert.Single(WorldTextProjector.CreateProjectedQuads(
            billboard,
            [glyph],
            new Vector2(100.0f, 50.0f),
            camera,
            1920,
            1080));
        TextProjectedQuadSource offsetQuad = Assert.Single(WorldTextProjector.CreateProjectedQuads(
            billboard,
            [glyph],
            new Vector2(100.0f, 50.0f),
            camera,
            1920,
            1080,
            new Vector2(2.0f, 3.0f)));

        Assert.Equal(baseQuad.TopLeft + new Vector2(2.0f, 3.0f), offsetQuad.TopLeft);
        Assert.Equal(Vector2.Distance(baseQuad.TopLeft, baseQuad.BottomLeft), Vector2.Distance(offsetQuad.TopLeft, offsetQuad.BottomLeft), precision: 4);
        Assert.Equal(Vector2.Distance(baseQuad.TopLeft, baseQuad.TopRight), Vector2.Distance(offsetQuad.TopLeft, offsetQuad.TopRight), precision: 4);
    }

    [Fact]
    public void WorldTextBehindCameraIsCulled()
    {
        var camera = new RenderCamera(Vector3.Zero, 0.0f, 0.0f);
        TextQuadSource glyph = new(new IntPtr(1), 0, 0, 100, 50, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White);
        WorldTextBillboard billboard = WorldTextBillboard.FixedFacing(
            "Behind",
            new Vector3(0.0f, 0.0f, 1.0f),
            1.0f,
            Vector2.Zero,
            WorldTextBasis.Identity,
            BlurgColor.White,
            new BlurgColor(0, 0, 0, 180),
            Vector2.One);

        IReadOnlyList<TextProjectedQuadSource> quads = WorldTextProjector.CreateProjectedQuads(
            billboard,
            [glyph],
            new Vector2(100.0f, 50.0f),
            camera,
            1920,
            1080);

        Assert.Empty(quads);
    }

    [Fact]
    public void FixedFacingWorldTextIsCulledWhenAuthoredPlaneIsEdgeOn()
    {
        var camera = new RenderCamera(Vector3.Zero, 0.0f, 0.0f);
        TextQuadSource glyph = new(new IntPtr(1), 0, 0, 100, 50, 0.0f, 0.0f, 1.0f, 1.0f, BlurgColor.White);
        WorldTextBillboard billboard = WorldTextBillboard.FixedFacing(
            "Edge",
            new Vector3(0.0f, 0.0f, -5.0f),
            1.0f,
            Vector2.Zero,
            new WorldTextBasis(Vector3.UnitZ, Vector3.UnitY),
            BlurgColor.White,
            new BlurgColor(0, 0, 0, 180),
            Vector2.One);

        IReadOnlyList<TextProjectedQuadSource> quads = WorldTextProjector.CreateProjectedQuads(
            billboard,
            [glyph],
            new Vector2(100.0f, 50.0f),
            camera,
            1920,
            1080);

        Assert.Empty(quads);
    }

    [Fact]
    public void SmokeLabelStateExposesExpectedTextAndPlacement()
    {
        TextSmokeLabelState state = TextSmokeLabelState.CreateDefault();

        Assert.Equal("Royale BlurgText", state.Text);
        Assert.Equal(new Vector2(24.0f, 24.0f), state.Position);
        Assert.Equal(new Vector2(1.0f, 1.0f), state.ShadowOffset);
        Assert.Equal(24.0f, state.FontSize);
        Assert.Equal(BlurgColor.White.Value, state.Foreground.Value);
    }

    [Fact]
    public void WorldSmokeLabelStateContainsCameraAndFixedFacingLabels()
    {
        WorldTextSmokeLabelState state = WorldTextSmokeLabelState.CreateDefault(Vector3.Zero, 1.8f);

        Assert.Equal(2, state.Labels.Count);
        Assert.Contains(state.Labels, label => label.Mode == WorldTextBillboardMode.CameraFacing && label.Text == "Training Dummy");
        Assert.Contains(state.Labels, label => label.Mode == WorldTextBillboardMode.FixedFacing && label.Text == "Fixed Label");
    }

    private static WorldTextBillboard FixedLabel(float worldHeight) =>
        WorldTextBillboard.FixedFacing(
            "Scale",
            new Vector3(0.0f, 0.0f, -5.0f),
            worldHeight,
            Vector2.Zero,
            WorldTextBasis.Identity,
            BlurgColor.White,
            new BlurgColor(0, 0, 0, 180),
            Vector2.One);

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
