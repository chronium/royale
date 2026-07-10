using System.Numerics;

namespace Royale.Client.Rendering.Meshes;

public sealed class StaticMeshAsset
{
    public StaticMeshAsset(string id, IReadOnlyList<StaticMeshPrimitive> primitives)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(primitives);
        if (primitives.Count == 0)
            throw new ArgumentException("A static mesh asset must contain at least one primitive.", nameof(primitives));

        Id = id;
        Primitives = primitives;
    }

    public string Id { get; }

    public IReadOnlyList<StaticMeshPrimitive> Primitives { get; }
}

public sealed record StaticMeshPrimitive(
    string DebugName,
    StaticMeshGeometry Geometry,
    StaticMeshMaterial Material);

public sealed record StaticMeshMaterial(
    Vector4 BaseColor,
    StaticMeshTextureData? BaseColorTexture)
{
    public static StaticMeshMaterial GrayBox { get; } =
        new(new Vector4(StaticMeshLightingConstants.DefaultAlbedo, 1.0f), BaseColorTexture: null);
}

public sealed record StaticMeshTextureData(
    string DebugName,
    string MimeType,
    byte[] Data);
