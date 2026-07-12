namespace Royale.Editor.Dialogs;

public interface IEditorFileDialogService
{
    void ShowOpenJsonDialog(nint parentWindow);
    void ShowSaveJsonDialog(nint parentWindow, string? defaultPath);
    void ShowOpenProjectDialog(nint parentWindow);
    void ShowDestinationParentDialog(nint parentWindow);
    bool TryDequeue(out EditorFileDialogResult result);
}

public enum EditorFileDialogKind { OpenMap, SaveMap, OpenProject, DestinationParent }
public readonly record struct EditorFileDialogResult(EditorFileDialogKind Kind, string? Path, string? Error);
