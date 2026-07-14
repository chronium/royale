using Royale.Editor.Mcp;
using Royale.Rendering;
using Royale.Rendering.Meshes;
using Royale.Rendering.Screenshots;

namespace Royale.Editor.Tests.Mcp;

public sealed class ModelContactSheetComposerTests
{
    [Theory]
    [InlineData(3, 1152)]
    [InlineData(4, 1536)]
    public void ComposesDeterministicTwoRowPngWithOrderedTilesAndSeparators(int columns, int expectedWidth)
    {
        GpuImageReadback[] tiles = Enumerable.Range(0, columns * 2)
            .Select(index => SolidTile((byte)(20 + index)))
            .ToArray();

        byte[] first = ModelContactSheetComposer.ComposePng(tiles, columns);
        byte[] second = ModelContactSheetComposer.ComposePng(tiles, columns);
        PngImage image = PngImageCodec.Decode(first);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, first[..8]);
        Assert.Equal(first, second);
        Assert.Equal(expectedWidth, image.Width);
        Assert.Equal(768, image.Height);
        for (int index = 0; index < tiles.Length; index++)
        {
            int x = (index % columns) * ModelContactSheetFraming.TileSize + 10;
            int y = (index / columns) * ModelContactSheetFraming.TileSize + 10;
            Assert.Equal((byte)(20 + index), Pixel(image, x, y));
        }
        Assert.Equal((byte)70, Pixel(image, ModelContactSheetFraming.TileSize, 10));
        Assert.Equal((byte)70, Pixel(image, 10, ModelContactSheetFraming.TileSize));
    }

    private static GpuImageReadback SolidTile(byte red)
    {
        byte[] rgba = new byte[ModelContactSheetFraming.TileSize * ModelContactSheetFraming.TileSize * 4];
        for (int offset = 0; offset < rgba.Length; offset += 4)
        {
            rgba[offset] = red;
            rgba[offset + 3] = byte.MaxValue;
        }
        return new GpuImageReadback(ModelContactSheetFraming.TileSize, ModelContactSheetFraming.TileSize, rgba);
    }

    private static byte Pixel(PngImage image, int x, int y) => image.RgbaBytes[(y * image.Width + x) * 4];
}
