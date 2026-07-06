using System.Numerics;
using System.Runtime.InteropServices;

namespace Royale.Client.Rendering;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct StaticMeshVertex(Vector3 Position, Vector3 Color);
