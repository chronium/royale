using System.Numerics;
using System.Runtime.InteropServices;

namespace Royale.Client.Rendering;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct StaticMeshInstanceShaderConstants(Matrix4x4 WorldViewProjection, Matrix4x4 WorldInverse);
