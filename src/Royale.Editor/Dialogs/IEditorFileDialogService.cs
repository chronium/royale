namespace Royale.Editor.Dialogs;

public interface IEditorFileDialogService
{
    void ShowOpenJsonDialog(nint parentWindow);
    void ShowSaveJsonDialog(nint parentWindow, string? defaultPath);
    void ShowOpenProjectDialog(nint parentWindow);
    void ShowDestinationParentDialog(nint parentWindow);
    void ShowOpenGlbDialog(nint parentWindow, bool collisionOnly = false);
    bool TryDequeue(out EditorFileDialogResult result);
}

public enum EditorFileDialogKind { OpenMap, SaveMap, OpenProject, DestinationParent, ImportModels, CollisionModel }
public readonly record struct EditorFileDialogResult(EditorFileDialogKind Kind, string? Path, string? Error, IReadOnlyList<string>? Paths = null);
