namespace Royale.Client.Rendering.Meshes;

public static class StaticMeshAssetPaths
{
    public const string KenneyPrototypeKitCrateRelativePath = "assets/meshes/kenney-prototype-kit/crate.glb";

    public static string GetKenneyPrototypeKitCratePath(string baseDirectory) =>
        Path.Combine(baseDirectory, KenneyPrototypeKitCrateRelativePath);
}
