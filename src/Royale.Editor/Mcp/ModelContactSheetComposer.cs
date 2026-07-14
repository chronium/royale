using Royale.Rendering;
using Royale.Rendering.Meshes;
using Royale.Rendering.Screenshots;

namespace Royale.Editor.Mcp;

public static class ModelContactSheetComposer
{
    private const byte SeparatorColor = 70;

    public static byte[] ComposePng(IReadOnlyList<GpuImageReadback> tiles, int columns)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (columns < 1 || tiles.Count != columns * 2)
            throw new ArgumentException("A contact sheet must contain exactly two complete rows.", nameof(tiles));

        int width = checked(columns * ModelContactSheetFraming.TileSize);
        int height = checked(2 * ModelContactSheetFraming.TileSize);
        byte[] pixels = new byte[checked(width * height * 4)];
        for (int index = 0; index < tiles.Count; index++)
        {
            GpuImageReadback tile = tiles[index];
            if (tile.Width != ModelContactSheetFraming.TileSize || tile.Height != ModelContactSheetFraming.TileSize)
                throw new InvalidDataException($"Contact-sheet tile {index} must be {ModelContactSheetFraming.TileSize}x{ModelContactSheetFraming.TileSize}.");
            CopyTile(tile.RgbaBytes, pixels, width, index % columns, index / columns);
        }

        DrawSeparators(pixels, width, height, columns);
        return PngImageCodec.Encode(pixels, width, height);
    }

    private static void CopyTile(byte[] source, byte[] destination, int sheetWidth, int column, int row)
    {
        int tileSize = ModelContactSheetFraming.TileSize;
        int sourceStride = tileSize * 4;
        int destinationX = column * tileSize * 4;
        int destinationY = row * tileSize;
        for (int y = 0; y < tileSize; y++)
            source.AsSpan(y * sourceStride, sourceStride)
                .CopyTo(destination.AsSpan((destinationY + y) * sheetWidth * 4 + destinationX, sourceStride));
    }

    private static void DrawSeparators(byte[] pixels, int width, int height, int columns)
    {
        for (int column = 1; column < columns; column++)
            DrawVertical(pixels, width, height, column * ModelContactSheetFraming.TileSize);
        DrawHorizontal(pixels, width, height, ModelContactSheetFraming.TileSize);
    }

    private static void DrawVertical(byte[] pixels, int width, int height, int x)
    {
        for (int y = 0; y < height; y++)
            SetPixel(pixels, width, x, y);
    }

    private static void DrawHorizontal(byte[] pixels, int width, int height, int y)
    {
        for (int x = 0; x < width; x++)
            SetPixel(pixels, width, x, y);
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y)
    {
        int offset = (y * width + x) * 4;
        pixels[offset] = SeparatorColor;
        pixels[offset + 1] = SeparatorColor;
        pixels[offset + 2] = SeparatorColor;
        pixels[offset + 3] = byte.MaxValue;
    }
}
