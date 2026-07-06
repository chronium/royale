namespace Royale.Client.Rendering.Meshes;

public sealed class StaticMeshRenderBatch
{
    public StaticMeshRenderBatch(string debugName, StaticMeshGeometry geometry, IReadOnlyList<StaticMeshInstance> instances)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(debugName);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(instances);

        DebugName = debugName;
        Geometry = geometry;
        Instances = instances;
    }

    public string DebugName { get; }

    public StaticMeshGeometry Geometry { get; }

    public IReadOnlyList<StaticMeshInstance> Instances { get; }
}
