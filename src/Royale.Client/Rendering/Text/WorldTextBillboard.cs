using System.Numerics;
using BlurgText;

namespace Royale.Client.Rendering.Text;

public readonly record struct WorldTextBillboard(
    string Text,
    Vector3 Position,
    float WorldHeight,
    Vector2 Anchor,
    WorldTextBillboardMode Mode,
    WorldTextBasis FixedBasis,
    BlurgColor Foreground,
    BlurgColor Shadow,
    Vector2 ShadowOffsetPixels)
{
    public const float DefaultFontSize = 48.0f;

    public static WorldTextBillboard CameraFacing(
        string text,
        Vector3 position,
        float worldHeight,
        Vector2 anchor,
        BlurgColor foreground,
        BlurgColor shadow,
        Vector2 shadowOffsetPixels) =>
        new(
            text,
            position,
            worldHeight,
            anchor,
            WorldTextBillboardMode.CameraFacing,
            WorldTextBasis.Identity,
            foreground,
            shadow,
            shadowOffsetPixels);

    public static WorldTextBillboard FixedFacing(
        string text,
        Vector3 position,
        float worldHeight,
        Vector2 anchor,
        WorldTextBasis fixedBasis,
        BlurgColor foreground,
        BlurgColor shadow,
        Vector2 shadowOffsetPixels) =>
        new(
            text,
            position,
            worldHeight,
            anchor,
            WorldTextBillboardMode.FixedFacing,
            fixedBasis,
            foreground,
            shadow,
            shadowOffsetPixels);
}
