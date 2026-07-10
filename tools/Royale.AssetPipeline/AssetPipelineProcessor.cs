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

        ModelAssetManifest generated = audience switch
        {
            AssetPipelineAudience.Client => BuildClient(source, sourceRoot, fullOutputRoot),
            AssetPipelineAudience.Server => BuildServer(source),
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
}
