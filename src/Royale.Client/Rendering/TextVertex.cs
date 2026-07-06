using System.Numerics;
using System.Runtime.InteropServices;

namespace Royale.Client.Rendering;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct TextVertex(Vector2 Position, Vector2 TexCoord, Vector4 Color)
{
    public const uint PositionOffset = 0;
    public static readonly int TexCoordOffset = Marshal.SizeOf<Vector2>();
    public static readonly int ColorOffset = Marshal.SizeOf<Vector2>() * 2;
    public static readonly int Stride = Marshal.SizeOf<TextVertex>();
}
