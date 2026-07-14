using System.Numerics;
using BlurgText;

namespace Royale.Rendering.Text;

public readonly record struct ScreenTextLabel(
    string Text,
    Vector2 Position,
    float FontSize,
    BlurgColor Foreground,
    BlurgColor Shadow,
    Vector2 ShadowOffset)
{
    public static ScreenTextLabel Create(string text, Vector2 position, float fontSize = 24.0f) => new(
        text,
        position,
        fontSize,
        BlurgColor.White,
        new BlurgColor(0, 0, 0, 180),
        new Vector2(1.0f, 1.0f));
}
