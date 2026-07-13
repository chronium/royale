namespace Royale.Editor.Playtest;

public sealed record EditorPlaytestRequest(
    string RepositoryRoot,
    string MapId,
    string MapFile,
    string ClientAssetRoot,
    string ServerAssetRoot,
    string Host,
    int Port,
    IDisposable? Artifacts = null);
