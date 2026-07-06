using BlurgText;

namespace Royale.Client.Rendering.Text;

public readonly record struct TextQuadSource(
    IntPtr TextureUserData,
    int X,
    int Y,
    int Width,
    int Height,
    float U0,
    float V0,
    float U1,
    float V1,
    BlurgColor Color);
