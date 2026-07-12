namespace Royale.Editor.Viewport;
public readonly record struct ViewportPixelSize(int Width, int Height)
{
    public static ViewportPixelSize FromLogical(float width, float height, float scaleX, float scaleY) => new(Math.Max(1, (int)MathF.Round(Math.Max(0, width) * Math.Max(0, scaleX))), Math.Max(1, (int)MathF.Round(Math.Max(0, height) * Math.Max(0, scaleY))));
}
