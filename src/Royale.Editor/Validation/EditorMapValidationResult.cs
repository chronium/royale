namespace Royale.Editor.Validation;

public sealed class EditorMapValidationResult : IDisposable
{
    private readonly string? temporaryRoot;
    private bool disposed;

    internal EditorMapValidationResult(
        EditorMapValidationReport report,
        string clientAssetRoot,
        string serverAssetRoot,
        string? temporaryRoot)
    {
        Report = report;
        ClientAssetRoot = clientAssetRoot;
        ServerAssetRoot = serverAssetRoot;
        this.temporaryRoot = temporaryRoot;
    }

    public EditorMapValidationReport Report { get; }

    public string ClientAssetRoot { get; }

    public string ServerAssetRoot { get; }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (temporaryRoot is not null && Directory.Exists(temporaryRoot))
            Directory.Delete(temporaryRoot, recursive: true);
    }
}
