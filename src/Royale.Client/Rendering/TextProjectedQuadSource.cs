using System.Numerics;
using BlurgText;

namespace Royale.Client.Rendering;

public readonly record struct TextProjectedQuadSource(
    IntPtr TextureUserData,
    Vector2 TopLeft,
    Vector2 TopRight,
    Vector2 BottomLeft,
    Vector2 BottomRight,
    float U0,
    float V0,
    float U1,
    float V1,
    BlurgColor Color);
