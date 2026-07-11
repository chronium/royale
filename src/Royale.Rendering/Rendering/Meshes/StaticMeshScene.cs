namespace Royale.Rendering.Meshes;

public sealed class StaticMeshScene
{
    private const string UnitBoxBatchDebugName = "unit-box";

    public StaticMeshScene(
        IReadOnlyList<StaticMeshInstance> unitBoxInstances,
        IReadOnlyList<StaticMeshRenderBatch> modelAssetBatches)
    {
        ArgumentNullException.ThrowIfNull(unitBoxInstances);
        ArgumentNullException.ThrowIfNull(modelAssetBatches);

        UnitBoxInstances = unitBoxInstances;
        ModelAssetBatches = modelAssetBatches;
    }

    public IReadOnlyList<StaticMeshInstance> UnitBoxInstances { get; }

    public IReadOnlyList<StaticMeshRenderBatch> ModelAssetBatches { get; }

    public IReadOnlyList<StaticMeshRenderBatch> CreateRenderBatches()
    {
        StaticMeshRenderBatch[] batches = new StaticMeshRenderBatch[ModelAssetBatches.Count + 1];
        batches[0] = new StaticMeshRenderBatch(UnitBoxBatchDebugName, UnitBoxMesh.Create(), UnitBoxInstances);

        for (int index = 0; index < ModelAssetBatches.Count; index++)
            batches[index + 1] = ModelAssetBatches[index];

        return batches;
    }
}
