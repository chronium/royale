using System.Numerics;
using System.Runtime.InteropServices;

namespace Royale.Client.Rendering.Debug;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct DebugLineVertex(Vector3 Position, Vector4 Color)
{
    public static readonly int Stride = Marshal.SizeOf<DebugLineVertex>();
    public const uint PositionOffset = 0;
    public static readonly int ColorOffset = Marshal.SizeOf<Vector3>();
}
