using System.Numerics;
using BlurgText;

namespace Royale.Client.Rendering;

public readonly record struct TextSmokeLabelState(
    string Text,
    Vector2 Position,
    Vector2 ShadowOffset,
    float FontSize,
    BlurgColor Foreground,
    BlurgColor Shadow)
{
    public static TextSmokeLabelState CreateDefault() => new(
        "Royale BlurgText",
        new Vector2(24.0f, 24.0f),
        new Vector2(1.0f, 1.0f),
        24.0f,
        BlurgColor.White,
        new BlurgColor(0, 0, 0, 160));
}
