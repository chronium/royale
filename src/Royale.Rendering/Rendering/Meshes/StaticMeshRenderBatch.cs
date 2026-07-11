namespace Royale.Rendering.Meshes;

public sealed class StaticMeshRenderBatch
{
    public StaticMeshRenderBatch(
        string debugName,
        StaticMeshGeometry geometry,
        IReadOnlyList<StaticMeshInstance> instances,
        StaticMeshMaterial? material = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(debugName);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(instances);

        DebugName = debugName;
        Geometry = geometry;
        Instances = instances;
        Material = material ?? StaticMeshMaterial.GrayBox;
    }

    public string DebugName { get; }

    public StaticMeshGeometry Geometry { get; }

    public IReadOnlyList<StaticMeshInstance> Instances { get; }

    public StaticMeshMaterial Material { get; }
}
