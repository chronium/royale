namespace Royale.Editor.Documents;

public sealed class EditorDocumentWorkflow
{
    private EditorDocumentTransition? pendingTransition;

    public EditorDocumentWorkflowState State { get; private set; }
    public EditorDocumentTransition? PendingTransition => pendingTransition;

    public EditorDocumentWorkflowResult Request(EditorDocumentTransition transition, bool isDirty)
    {
        if (State != EditorDocumentWorkflowState.Idle)
            return EditorDocumentWorkflowResult.None;

        if (!isDirty)
            return EditorDocumentWorkflowResult.Continue(transition);

        pendingTransition = transition;
        State = EditorDocumentWorkflowState.AwaitingUnsavedDecision;
        return EditorDocumentWorkflowResult.ShowUnsavedPrompt;
    }

    public EditorDocumentWorkflowResult Save()
    {
        if (State != EditorDocumentWorkflowState.AwaitingUnsavedDecision)
            return EditorDocumentWorkflowResult.None;

        State = EditorDocumentWorkflowState.AwaitingSaveCompletion;
        return EditorDocumentWorkflowResult.SaveDocument;
    }

    public EditorDocumentWorkflowResult Discard()
    {
        if (State != EditorDocumentWorkflowState.AwaitingUnsavedDecision)
            return EditorDocumentWorkflowResult.None;

        return CompletePendingTransition();
    }

    public EditorDocumentWorkflowResult Cancel()
    {
        if (State != EditorDocumentWorkflowState.AwaitingUnsavedDecision)
            return EditorDocumentWorkflowResult.None;

        Reset();
        return EditorDocumentWorkflowResult.None;
    }

    public EditorDocumentWorkflowResult SaveSucceeded()
    {
        if (State != EditorDocumentWorkflowState.AwaitingSaveCompletion)
            return EditorDocumentWorkflowResult.None;

        return CompletePendingTransition();
    }

    public EditorDocumentWorkflowResult SaveFailed() => ReturnToUnsavedPrompt();

    public EditorDocumentWorkflowResult SaveAsCancelled() => ReturnToUnsavedPrompt();

    private EditorDocumentWorkflowResult ReturnToUnsavedPrompt()
    {
        if (State != EditorDocumentWorkflowState.AwaitingSaveCompletion)
            return EditorDocumentWorkflowResult.None;

        State = EditorDocumentWorkflowState.AwaitingUnsavedDecision;
        return EditorDocumentWorkflowResult.ShowUnsavedPrompt;
    }

    private EditorDocumentWorkflowResult CompletePendingTransition()
    {
        EditorDocumentTransition transition = pendingTransition
            ?? throw new InvalidOperationException("A document transition was not pending.");
        Reset();
        return EditorDocumentWorkflowResult.Continue(transition);
    }

    private void Reset()
    {
        pendingTransition = null;
        State = EditorDocumentWorkflowState.Idle;
    }
}

public enum EditorDocumentTransition
{
    NewProject,
    OpenProject,
    OpenMap,
    Convert,
    Close,
}

public enum EditorDocumentWorkflowState
{
    Idle,
    AwaitingUnsavedDecision,
    AwaitingSaveCompletion,
}

public enum EditorDocumentWorkflowAction
{
    None,
    ShowUnsavedPrompt,
    SaveDocument,
    ContinueTransition,
}

public readonly record struct EditorDocumentWorkflowResult(
    EditorDocumentWorkflowAction Action,
    EditorDocumentTransition? Transition = null)
{
    public static EditorDocumentWorkflowResult None => new(EditorDocumentWorkflowAction.None);
    public static EditorDocumentWorkflowResult ShowUnsavedPrompt => new(EditorDocumentWorkflowAction.ShowUnsavedPrompt);
    public static EditorDocumentWorkflowResult SaveDocument => new(EditorDocumentWorkflowAction.SaveDocument);
    public static EditorDocumentWorkflowResult Continue(EditorDocumentTransition transition) =>
        new(EditorDocumentWorkflowAction.ContinueTransition, transition);
}
