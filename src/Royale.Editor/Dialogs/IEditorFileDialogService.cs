namespace Royale.Editor.Dialogs;

public interface IEditorFileDialogService
{
    void ShowOpenJsonDialog(nint parentWindow);
    void ShowSaveJsonDialog(nint parentWindow, string? defaultPath);
    bool TryDequeue(out EditorFileDialogResult result);
}

public enum EditorFileDialogKind { Open, Save }
public readonly record struct EditorFileDialogResult(EditorFileDialogKind Kind, string? Path, string? Error);
