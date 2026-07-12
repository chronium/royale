using Royale.Content.Models;
using Royale.Editor.Projects.Assets;

namespace Royale.Editor.Workspace.Assets;

public sealed class PendingAssetImportState
{
    private readonly byte[] assetIdBuffer = new byte[128];
    public PendingAssetImportState(string sourcePath, IEnumerable<string> reservedIds)
    {
        SourcePath = sourcePath;
        AssetId = UniqueId(AssetIdSlug.FromFileName(sourcePath), reservedIds);
        try
        {
            ExternalResourceCount = GlbExternalResourceInspector.Inspect(sourcePath).Count;
        }
        catch (Exception ex)
        {
            Diagnostic = ex.Message;
        }
    }

    public string SourcePath { get; }
    public bool Include { get; set; } = true;
    public string AssetId
    {
        get
        {
            int length = Array.IndexOf(assetIdBuffer, (byte)0);
            return System.Text.Encoding.UTF8.GetString(assetIdBuffer, 0, length < 0 ? assetIdBuffer.Length : length);
        }
        set
        {
            Array.Clear(assetIdBuffer);
            int count = System.Text.Encoding.UTF8.GetBytes(value, assetIdBuffer);
            if (count == assetIdBuffer.Length)
                assetIdBuffer[^1] = 0;
        }
    }
    public byte[] AssetIdBuffer => assetIdBuffer;
    public ModelCollisionMode CollisionMode { get; set; } = ModelCollisionMode.Convex;
    public string? SeparateCollisionPath { get; set; }
    public int ExternalResourceCount { get; }
    public string? Diagnostic { get; private set; }

    public PendingAssetImport ToCommand() => new(SourcePath, AssetId, Include, CollisionMode, SeparateCollisionPath);

    public void Validate(ISet<string> ids)
    {
        Diagnostic = null;
        if (!Include)
            return;
        if (AssetId.Length == 0 || AssetIdSlug.FromFileName(AssetId) != AssetId || !ids.Add(AssetId))
            Diagnostic = "Asset ID must be a globally unique lowercase portable slug.";
        else if (CollisionMode == ModelCollisionMode.SeparateMesh && string.IsNullOrEmpty(SeparateCollisionPath))
            Diagnostic = "Choose one GLB for the separate collision mesh.";
    }

    private static string UniqueId(string seed, IEnumerable<string> reserved)
    {
        var ids = reserved.ToHashSet(StringComparer.Ordinal);
        string candidate = seed.Length == 0 ? "model" : seed;
        for (int suffix = 2; ids.Contains(candidate); suffix++)
            candidate = $"{seed}-{suffix}";
        return candidate;
    }
}
