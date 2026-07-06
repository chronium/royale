namespace Royale.Client.Rendering.Text;

internal sealed class TextAtlasUpdate
{
    public TextAtlasUpdate(IntPtr textureUserData, byte[] pixels, int x, int y, int width, int height)
    {
        TextureUserData = textureUserData;
        Pixels = pixels;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public IntPtr TextureUserData { get; }

    public byte[] Pixels { get; }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }
}
