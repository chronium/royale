namespace Royale.Rendering.Meshes;

public sealed class StaticMeshGeometry
{
    public StaticMeshGeometry(IReadOnlyList<StaticMeshVertex> vertices, IReadOnlyList<ushort> indices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);

        Vertices = vertices;
        Indices = indices;
    }

    public IReadOnlyList<StaticMeshVertex> Vertices { get; }

    public IReadOnlyList<ushort> Indices { get; }
}
