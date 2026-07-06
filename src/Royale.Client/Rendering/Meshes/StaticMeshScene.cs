namespace Royale.Client.Rendering.Meshes;

public sealed class StaticMeshScene
{
    private const string UnitBoxBatchDebugName = "unit-box";

    public StaticMeshScene(
        IReadOnlyList<StaticMeshInstance> unitBoxInstances,
        IReadOnlyList<StaticMeshRenderBatch> smokeMeshBatches)
    {
        ArgumentNullException.ThrowIfNull(unitBoxInstances);
        ArgumentNullException.ThrowIfNull(smokeMeshBatches);

        UnitBoxInstances = unitBoxInstances;
        SmokeMeshBatches = smokeMeshBatches;
    }

    public IReadOnlyList<StaticMeshInstance> UnitBoxInstances { get; }

    public IReadOnlyList<StaticMeshRenderBatch> SmokeMeshBatches { get; }

    public IReadOnlyList<StaticMeshRenderBatch> CreateRenderBatches()
    {
        StaticMeshRenderBatch[] batches = new StaticMeshRenderBatch[SmokeMeshBatches.Count + 1];
        batches[0] = new StaticMeshRenderBatch(UnitBoxBatchDebugName, UnitBoxMesh.Create(), UnitBoxInstances);

        for (int index = 0; index < SmokeMeshBatches.Count; index++)
            batches[index + 1] = SmokeMeshBatches[index];

        return batches;
    }
}
