using System.Text.Json;
using Royale.Content;

namespace Royale.AssetPipeline;

public enum AssetPipelineAudience
{
    Client,
    Server,
}

public static class AssetPipelineProcessor
{
    public static void Build(
        string manifestPath,
        string sourceRoot,
        string outputRoot,
        AssetPipelineAudience audience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        ModelAssetManifest source = ModelAssetManifestLoader.LoadSource(manifestPath, sourceRoot);
        string fullOutputRoot = Path.GetFullPath(outputRoot);

        if (Directory.Exists(fullOutputRoot))
            Directory.Delete(fullOutputRoot, recursive: true);
        Directory.CreateDirectory(fullOutputRoot);

        ModelAssetManifest withCollisionArtifacts = BuildCollisionArtifacts(source, sourceRoot, fullOutputRoot);
        ModelAssetManifest generated = audience switch
        {
            AssetPipelineAudience.Client => BuildClient(withCollisionArtifacts, sourceRoot, fullOutputRoot),
            AssetPipelineAudience.Server => BuildServer(withCollisionArtifacts),
            _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown asset pipeline audience."),
        };

        WriteManifest(Path.Combine(fullOutputRoot, ContentCatalog.ModelAssetManifestFileName), generated);
    }

    private static ModelAssetManifest BuildClient(
        ModelAssetManifest source,
        string sourceRoot,
        string outputRoot)
    {
        foreach (ModelAssetDefinition asset in source.Assets.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            if (asset.Render is null)
                continue;

            CopySource(asset.Render.Source, sourceRoot, outputRoot);
            foreach (string resource in asset.Render.Resources.Order(StringComparer.Ordinal))
                CopySource(resource, sourceRoot, outputRoot);
        }

        return NormalizeManifest(source, includeRender: true, includeOnlyCollisionAssets: false);
    }

    private static ModelAssetManifest BuildServer(ModelAssetManifest source) =>
        NormalizeManifest(source, includeRender: false, includeOnlyCollisionAssets: true);

    private static ModelAssetManifest BuildCollisionArtifacts(
        ModelAssetManifest source,
        string sourceRoot,
        string outputRoot)
    {
        var assets = new List<ModelAssetDefinition>(source.Assets.Count);
        foreach (ModelAssetDefinition asset in source.Assets)
        {
            if (asset.Collision.Mode == ModelCollisionMode.None)
            {
                assets.Add(asset);
                continue;
            }

            string relativeSource = asset.Collision.Mode switch
            {
                ModelCollisionMode.Convex or ModelCollisionMode.TriangleMesh =>
                    asset.Render?.Source
                    ?? throw new InvalidDataException($"Asset '{asset.Id}' {asset.Collision.Mode} collision requires a render GLB source."),
                ModelCollisionMode.SeparateMesh =>
                    asset.Collision.Source
                    ?? throw new InvalidDataException($"Asset '{asset.Id}' separateMesh collision requires a collision GLB source."),
                _ => throw new InvalidDataException($"Asset '{asset.Id}' collision mode '{asset.Collision.Mode}' is not supported by this asset pipeline version."),
            };
            string sourcePath = ModelAssetManifestLoader.ResolveSourcePath(sourceRoot, relativeSource);
            CollisionTriangleGeometry geometry = SimpleMeshCollisionGeometryExtractor.LoadFromFile(
                sourcePath,
                discardDegenerateTriangles: asset.Collision.Mode != ModelCollisionMode.Convex);
            ModelCollisionArtifact artifact = asset.Collision.Mode == ModelCollisionMode.Convex
                ? ConvexCollisionArtifactGenerator.Generate(geometry, asset.Id)
                : TriangleMeshCollisionArtifactGenerator.Generate(geometry, asset.Id);
            string relativeArtifactPath = $"collision/{asset.Id}.json";
            WriteCollisionArtifact(Path.Combine(outputRoot, relativeArtifactPath), artifact);
            assets.Add(asset with
            {
                Collision = asset.Collision with { Artifact = relativeArtifactPath },
            });
        }

        return source with { Assets = assets };
    }

    private static ModelAssetManifest NormalizeManifest(
        ModelAssetManifest source,
        bool includeRender,
        bool includeOnlyCollisionAssets)
    {
        IEnumerable<ModelAssetDefinition> assets = source.Assets
            .Where(asset => !includeOnlyCollisionAssets || asset.Collision.Mode != ModelCollisionMode.None)
            .OrderBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(asset => asset with
            {
                Collision = asset.Collision with { Source = null },
                Render = includeRender && asset.Render is not null
                    ? asset.Render with
                    {
                        Resources = asset.Render.Resources.Order(StringComparer.Ordinal).ToList(),
                    }
                    : null,
            });

        return new ModelAssetManifest
        {
            Version = ModelAssetManifest.CurrentVersion,
            Assets = assets.ToList(),
        };
    }

    private static void CopySource(string relativePath, string sourceRoot, string outputRoot)
    {
        string sourcePath = ModelAssetManifestLoader.ResolveSourcePath(sourceRoot, relativePath);
        string destinationPath = Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void WriteManifest(string path, ModelAssetManifest manifest)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            ModelAssetManifestLoader.CreateSerializerOptions(writeIndented: true));
        using FileStream stream = File.Create(path);
        stream.Write(json);
        stream.WriteByte((byte)'\n');
    }

    private static void WriteCollisionArtifact(string path, ModelCollisionArtifact artifact)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            artifact,
            ModelCollisionArtifactLoader.CreateSerializerOptions(writeIndented: true));
        using FileStream stream = File.Create(path);
        stream.Write(json);
        stream.WriteByte((byte)'\n');
    }
}
