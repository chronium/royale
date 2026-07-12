using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace Royale.Editor.Dialogs;

public sealed unsafe class SdlEditorFileDialogService : IEditorFileDialogService
{
    private readonly ConcurrentQueue<EditorFileDialogResult> results = new();
    private GCHandle callbackHandle;
    private EditorFileDialogKind pendingKind;

    public void ShowOpenJsonDialog(nint parentWindow) => Show(EditorFileDialogKind.OpenMap, parentWindow, null);
    public void ShowSaveJsonDialog(nint parentWindow, string? defaultPath) => Show(EditorFileDialogKind.SaveMap, parentWindow, defaultPath);
    public void ShowOpenProjectDialog(nint parentWindow) => ShowFolder(EditorFileDialogKind.OpenProject, parentWindow);
    public void ShowDestinationParentDialog(nint parentWindow) => ShowFolder(EditorFileDialogKind.DestinationParent, parentWindow);
    public void ShowOpenGlbDialog(nint parentWindow, bool collisionOnly = false) => ShowGlb(
        collisionOnly ? EditorFileDialogKind.CollisionModel : EditorFileDialogKind.ImportModels,
        parentWindow,
        allowMany: !collisionOnly);
    public bool TryDequeue(out EditorFileDialogResult result) => results.TryDequeue(out result);

    private void Show(EditorFileDialogKind kind, nint parentWindow, string? defaultPath)
    {
        if (callbackHandle.IsAllocated) return;
        pendingKind = kind;
        callbackHandle = GCHandle.Alloc(this);
        byte[] name = "JSON map\0"u8.ToArray(); byte[] pattern = "json\0"u8.ToArray();
        byte[]? location = defaultPath is null ? null : System.Text.Encoding.UTF8.GetBytes(defaultPath + '\0');
        fixed (byte* namePtr = name) fixed (byte* patternPtr = pattern) fixed (byte* locationPtr = location)
        {
            var filter = new SDL_DialogFileFilter { name = namePtr, pattern = patternPtr };
            if (kind == EditorFileDialogKind.OpenMap) SDL_ShowOpenFileDialog(&Callback, GCHandle.ToIntPtr(callbackHandle), (SDL_Window*)parentWindow, &filter, 1, (byte*)null, false);
            else SDL_ShowSaveFileDialog(&Callback, GCHandle.ToIntPtr(callbackHandle), (SDL_Window*)parentWindow, &filter, 1, locationPtr);
        }
    }

    private void ShowFolder(EditorFileDialogKind kind, nint parentWindow)
    {
        if (callbackHandle.IsAllocated)
            return;
        pendingKind = kind;
        callbackHandle = GCHandle.Alloc(this);
        SDL_ShowOpenFolderDialog(&Callback, GCHandle.ToIntPtr(callbackHandle), (SDL_Window*)parentWindow, (byte*)null, false);
    }

    private void ShowGlb(EditorFileDialogKind kind, nint parentWindow, bool allowMany)
    {
        if (callbackHandle.IsAllocated)
            return;
        pendingKind = kind;
        callbackHandle = GCHandle.Alloc(this);
        byte[] name = "GLB model\0"u8.ToArray();
        byte[] pattern = "glb\0"u8.ToArray();
        fixed (byte* namePtr = name)
        fixed (byte* patternPtr = pattern)
        {
            var filter = new SDL_DialogFileFilter { name = namePtr, pattern = patternPtr };
            SDL_ShowOpenFileDialog(&Callback, GCHandle.ToIntPtr(callbackHandle), (SDL_Window*)parentWindow, &filter, 1, (byte*)null, allowMany);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Callback(nint userdata, byte** filelist, int filter)
    {
        GCHandle handle = GCHandle.FromIntPtr(userdata);
        var service = (SdlEditorFileDialogService)handle.Target!;
        string? error = filelist is null ? SDL_GetError() : null;
        var paths = new List<string>();
        if (filelist is not null)
            for (int index = 0; filelist[index] is not null; index++)
                paths.Add(PtrToStringUTF8(filelist[index])!);
        string? path = paths.Count == 0 ? null : paths[0];
        service.results.Enqueue(new(service.pendingKind, path, string.IsNullOrEmpty(error) ? null : error, paths));
        handle.Free(); service.callbackHandle = default;
    }
}
