using System.Numerics;

namespace Royale.Client.Rendering.Meshes;

public readonly record struct StaticMeshInstance(Matrix4x4 Transform, string? DebugName = null);
