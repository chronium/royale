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

    public void ShowOpenJsonDialog(nint parentWindow) => Show(EditorFileDialogKind.Open, parentWindow, null);
    public void ShowSaveJsonDialog(nint parentWindow, string? defaultPath) => Show(EditorFileDialogKind.Save, parentWindow, defaultPath);
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
            if (kind == EditorFileDialogKind.Open) SDL_ShowOpenFileDialog(&Callback, GCHandle.ToIntPtr(callbackHandle), (SDL_Window*)parentWindow, &filter, 1, (byte*)null, false);
            else SDL_ShowSaveFileDialog(&Callback, GCHandle.ToIntPtr(callbackHandle), (SDL_Window*)parentWindow, &filter, 1, locationPtr);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Callback(nint userdata, byte** filelist, int filter)
    {
        GCHandle handle = GCHandle.FromIntPtr(userdata);
        var service = (SdlEditorFileDialogService)handle.Target!;
        string? error = filelist is null ? SDL_GetError() : null;
        string? path = filelist is not null && *filelist is not null ? PtrToStringUTF8(*filelist) : null;
        service.results.Enqueue(new(service.pendingKind, path, string.IsNullOrEmpty(error) ? null : error));
        handle.Free(); service.callbackHandle = default;
    }
}
