namespace Royale.Editor.Validation;

public sealed record EditorMapValidationStage(string Category, bool Success, string Message);

public sealed record EditorMapValidationReport(
    long DocumentRevision,
    string? AssetManifestFingerprint,
    IReadOnlyList<EditorMapValidationStage> Stages)
{
    public bool Success => Stages.All(stage => stage.Success);

    public bool IsCurrent(long documentRevision, string? assetManifestFingerprint) =>
        DocumentRevision == documentRevision &&
        string.Equals(AssetManifestFingerprint, assetManifestFingerprint, StringComparison.Ordinal);
}
