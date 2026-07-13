using Royale.Editor.Documents;

namespace Royale.Editor.Viewport;

public sealed class EditorTransformManipulation
{
    private EditorEntityIdentity identity;
    private EditorEntityTransform before;

    public bool IsActive { get; private set; }

    public void Begin(EditorMapDocument document, EditorEntityIdentity target)
    {
        if (IsActive)
            throw new InvalidOperationException("A transform manipulation is already active.");
        before = EditorEntityTransforms.Get(document, target);
        identity = target;
        IsActive = true;
    }

    public void Preview(EditorMapDocument document, EditorEntityTransform value)
    {
        EnsureActive();
        EditorEntityTransforms.Set(document, identity, value, validate: false);
    }

    public bool Complete(EditorMapDocument document, out string? error)
    {
        EnsureActive();
        EditorEntityTransform after = EditorEntityTransforms.Get(document, identity);
        try
        {
            EditorEntityTransforms.ValidateTransform(identity.Kind, after);
        }
        catch (ArgumentException exception)
        {
            EditorEntityTransforms.Set(document, identity, before);
            IsActive = false;
            error = exception.Message;
            return false;
        }

        IsActive = false;
        error = null;
        if (before.NearlyEquals(after))
            return false;

        document.Execute(new SetEntityTransformCommand(identity.EditorId, before, after));
        return true;
    }

    public bool Cancel(EditorMapDocument document)
    {
        if (!IsActive)
            return false;
        EditorEntityTransforms.Set(document, identity, before);
        IsActive = false;
        return true;
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException("No transform manipulation is active.");
    }
}
