using System.Numerics;
using System.Runtime.InteropServices;

namespace Royale.Client.Rendering.Meshes;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct StaticMeshVertex(
    Vector3 Position,
    Vector3 Normal,
    Vector2 TextureCoordinate = default)
{
    public const int PositionOffset = 0;
    public static int NormalOffset => Marshal.SizeOf<Vector3>();
    public static int TextureCoordinateOffset => Marshal.SizeOf<Vector3>() * 2;
    public static int Stride => Marshal.SizeOf<Vector3>() * 2 + Marshal.SizeOf<Vector2>();
}
