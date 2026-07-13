using Royale.Editor.Documents;
using Royale.Editor.Persistence;
using Royale.Editor.Projects;
using Royale.Editor.Validation;

namespace Royale.Editor.Playtest;

public enum EditorSaveAndLaunchStatus
{
    ValidationFailed,
    AwaitingSaveAs,
    Ready,
    Cancelled,
}

public sealed record EditorSaveAndLaunchOutcome(
    EditorSaveAndLaunchStatus Status,
    EditorMapValidationReport Report,
    EditorPlaytestRequest? Request = null);

public sealed class EditorSaveAndLaunchCoordinator : IDisposable
{
    private readonly Func<string> resolveRepositoryRoot;
    private EditorMapValidationResult? pendingValidation;
    private EditorMapDocument? pendingDocument;

    public EditorSaveAndLaunchCoordinator(Func<string> resolveRepositoryRoot)
    {
        this.resolveRepositoryRoot = resolveRepositoryRoot
            ?? throw new ArgumentNullException(nameof(resolveRepositoryRoot));
    }

    public bool AwaitingSaveAs => pendingValidation is not null;

    public EditorSaveAndLaunchOutcome Begin(
        EditorMapDocument document,
        EditorProjectSession? projectSession)
    {
        ArgumentNullException.ThrowIfNull(document);
        Cancel();

        EditorMapValidationResult validation = EditorMapValidator.Validate(document, projectSession);
        EditorMapValidationReport report = validation.Report;
        if (!report.Success)
        {
            validation.Dispose();
            return new EditorSaveAndLaunchOutcome(EditorSaveAndLaunchStatus.ValidationFailed, report);
        }

        try
        {
            if (projectSession is not null)
            {
                projectSession.Save();
                return Ready(document, projectSession.Project.Paths.Map, validation);
            }

            if (document.RequiresSaveAs || document.SourcePath is null)
            {
                pendingValidation = validation;
                pendingDocument = document;
                return new EditorSaveAndLaunchOutcome(EditorSaveAndLaunchStatus.AwaitingSaveAs, report);
            }

            EditorMapPersistence.Save(document, document.SourcePath, checkExternalChange: true);
            return Ready(document, document.SourcePath, validation);
        }
        catch
        {
            validation.Dispose();
            throw;
        }
    }

    public EditorSaveAndLaunchOutcome CompleteSaveAs(string? path)
    {
        EditorMapValidationResult validation = pendingValidation
            ?? throw new InvalidOperationException("Save and Launch is not awaiting a Save As destination.");
        EditorMapDocument document = pendingDocument
            ?? throw new InvalidOperationException("Save and Launch lost its pending document.");
        pendingValidation = null;
        pendingDocument = null;

        if (path is null)
        {
            EditorMapValidationReport report = validation.Report;
            validation.Dispose();
            return new EditorSaveAndLaunchOutcome(EditorSaveAndLaunchStatus.Cancelled, report);
        }

        try
        {
            EditorMapPersistence.Save(document, path, checkExternalChange: false);
            return Ready(document, document.SourcePath!, validation);
        }
        catch
        {
            validation.Dispose();
            throw;
        }
    }

    public void Cancel()
    {
        pendingValidation?.Dispose();
        pendingValidation = null;
        pendingDocument = null;
    }

    private EditorSaveAndLaunchOutcome Ready(
        EditorMapDocument document,
        string mapFile,
        EditorMapValidationResult validation)
    {
        try
        {
            var request = new EditorPlaytestRequest(
                resolveRepositoryRoot(),
                document.Map.Id,
                mapFile,
                validation.ClientAssetRoot,
                validation.ServerAssetRoot,
                "127.0.0.1",
                7777,
                validation);
            return new EditorSaveAndLaunchOutcome(
                EditorSaveAndLaunchStatus.Ready,
                validation.Report,
                request);
        }
        catch
        {
            validation.Dispose();
            throw;
        }
    }

    public void Dispose() => Cancel();
}
