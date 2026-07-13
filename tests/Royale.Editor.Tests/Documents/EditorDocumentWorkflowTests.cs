using Royale.Editor.Documents;

namespace Royale.Editor.Tests.Documents;

public sealed class EditorDocumentWorkflowTests
{
    public static TheoryData<EditorDocumentTransition> Transitions => new()
    {
        EditorDocumentTransition.NewProject,
        EditorDocumentTransition.OpenProject,
        EditorDocumentTransition.OpenMap,
        EditorDocumentTransition.Convert,
        EditorDocumentTransition.Close,
    };

    [Theory]
    [MemberData(nameof(Transitions))]
    public void CleanDocumentContinuesEveryTransition(EditorDocumentTransition transition)
    {
        var workflow = new EditorDocumentWorkflow();

        EditorDocumentWorkflowResult result = workflow.Request(transition, isDirty: false);

        AssertContinue(result, transition);
        AssertIdle(workflow);
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void DirtyDocumentPromptsAndInPlaceSaveSuccessContinues(EditorDocumentTransition transition)
    {
        EditorDocumentWorkflow workflow = BeginSave(transition);

        EditorDocumentWorkflowResult result = workflow.SaveSucceeded();

        AssertContinue(result, transition);
        AssertIdle(workflow);
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void DirtyDocumentPromptsAndSaveAsSuccessContinues(EditorDocumentTransition transition)
    {
        EditorDocumentWorkflow workflow = BeginSave(transition);

        EditorDocumentWorkflowResult result = workflow.SaveSucceeded();

        AssertContinue(result, transition);
        AssertIdle(workflow);
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void DirtyDocumentDiscardContinuesAndCancelAbandons(EditorDocumentTransition transition)
    {
        var discardWorkflow = new EditorDocumentWorkflow();
        discardWorkflow.Request(transition, isDirty: true);

        AssertContinue(discardWorkflow.Discard(), transition);
        AssertIdle(discardWorkflow);

        var cancelWorkflow = new EditorDocumentWorkflow();
        cancelWorkflow.Request(transition, isDirty: true);

        Assert.Equal(EditorDocumentWorkflowAction.None, cancelWorkflow.Cancel().Action);
        AssertIdle(cancelWorkflow);
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void SaveFailureAndSaveAsCancellationReturnToPrompt(EditorDocumentTransition transition)
    {
        var failedWorkflow = BeginSave(transition);

        Assert.Equal(EditorDocumentWorkflowAction.ShowUnsavedPrompt, failedWorkflow.SaveFailed().Action);
        Assert.Equal(EditorDocumentWorkflowState.AwaitingUnsavedDecision, failedWorkflow.State);
        Assert.Equal(transition, failedWorkflow.PendingTransition);

        var cancelledWorkflow = BeginSave(transition);

        Assert.Equal(EditorDocumentWorkflowAction.ShowUnsavedPrompt, cancelledWorkflow.SaveAsCancelled().Action);
        Assert.Equal(EditorDocumentWorkflowState.AwaitingUnsavedDecision, cancelledWorkflow.State);
        Assert.Equal(transition, cancelledWorkflow.PendingTransition);
    }

    [Fact]
    public void RepeatedRequestsAndOutOfOrderResponsesDoNotReplacePendingTransition()
    {
        var workflow = new EditorDocumentWorkflow();
        workflow.Request(EditorDocumentTransition.OpenProject, isDirty: true);

        Assert.Equal(
            EditorDocumentWorkflowAction.None,
            workflow.Request(EditorDocumentTransition.Close, isDirty: false).Action);
        Assert.Equal(EditorDocumentWorkflowAction.None, workflow.SaveSucceeded().Action);
        Assert.Equal(EditorDocumentTransition.OpenProject, workflow.PendingTransition);

        workflow.Save();
        Assert.Equal(
            EditorDocumentWorkflowAction.None,
            workflow.Request(EditorDocumentTransition.NewProject, isDirty: true).Action);
        Assert.Equal(EditorDocumentWorkflowAction.None, workflow.Discard().Action);

        AssertContinue(workflow.SaveSucceeded(), EditorDocumentTransition.OpenProject);
        AssertIdle(workflow);
    }

    private static EditorDocumentWorkflow BeginSave(EditorDocumentTransition transition)
    {
        var workflow = new EditorDocumentWorkflow();
        Assert.Equal(EditorDocumentWorkflowAction.ShowUnsavedPrompt, workflow.Request(transition, isDirty: true).Action);
        Assert.Equal(EditorDocumentWorkflowAction.SaveDocument, workflow.Save().Action);
        Assert.Equal(EditorDocumentWorkflowState.AwaitingSaveCompletion, workflow.State);
        return workflow;
    }

    private static void AssertContinue(EditorDocumentWorkflowResult result, EditorDocumentTransition transition)
    {
        Assert.Equal(EditorDocumentWorkflowAction.ContinueTransition, result.Action);
        Assert.Equal(transition, result.Transition);
    }

    private static void AssertIdle(EditorDocumentWorkflow workflow)
    {
        Assert.Equal(EditorDocumentWorkflowState.Idle, workflow.State);
        Assert.Null(workflow.PendingTransition);
    }
}
