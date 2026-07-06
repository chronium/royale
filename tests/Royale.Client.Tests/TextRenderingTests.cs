using System.Numerics;
using System.Runtime.InteropServices;
using BlurgText;
using Royale.Client.Rendering;

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
    public void SmokeLabelStateExposesExpectedTextAndPlacement()
    {
        TextSmokeLabelState state = TextSmokeLabelState.CreateDefault();

        Assert.Equal("Royale BlurgText", state.Text);
        Assert.Equal(new Vector2(24.0f, 24.0f), state.Position);
        Assert.Equal(new Vector2(1.0f, 1.0f), state.ShadowOffset);
        Assert.Equal(24.0f, state.FontSize);
        Assert.Equal(BlurgColor.White.Value, state.Foreground.Value);
    }
}
