using System.Security.Cryptography;
using System.Text;
using Royale.Content.Models;
using Royale.Rendering.Meshes;

namespace Royale.Editor.Workspace.Assets;

public static class ModelThumbnailFingerprint
{
    public const string RendererVersion = "model-thumbnail-v1";

    public static string Calculate(
        string assetId,
        ModelRenderAssetDefinition render,
        string sourceRoot,
        string rendererVersion = RendererVersion,
        string? settingsSignature = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentNullException.ThrowIfNull(render);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Add(hash, rendererVersion);
        Add(hash, assetId);
        Add(hash, render.Source);
        foreach (string resource in render.Resources.Order(StringComparer.Ordinal))
            Add(hash, resource);
        Add(hash, settingsSignature ?? $"{ModelThumbnailFraming.Resolution}|{RenderCameraSettings()}|{ModelThumbnailFraming.Padding:R}|neutral-gray:0.18|directional-light:v1");
        AddFile(hash, sourceRoot, render.Source);
        foreach (string resource in render.Resources.Order(StringComparer.Ordinal))
            AddFile(hash, sourceRoot, resource);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string RenderCameraSettings() => "fov:60|view:1,0.75,1|bounds-near-far";

    private static void AddFile(IncrementalHash hash, string root, string relativePath)
    {
        Add(hash, relativePath.Replace('\\', '/'));
        string path = ModelAssetManifestLoader.ResolveSourcePath(root, relativePath);
        hash.AppendData(File.ReadAllBytes(path));
    }

    private static void Add(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
