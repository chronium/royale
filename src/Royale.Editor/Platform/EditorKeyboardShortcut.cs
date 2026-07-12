using SDL;

namespace Royale.Editor.Platform;

public enum EditorKeyboardShortcut
{
    None,
    Open,
    Save,
    SaveAs,
    Undo,
    Redo,
}

public static class EditorKeyboardShortcutResolver
{
    public static EditorKeyboardShortcut Resolve(SDL_Keycode key, SDL_Keymod modifiers)
    {
        bool command = (modifiers & (SDL_Keymod.SDL_KMOD_CTRL | SDL_Keymod.SDL_KMOD_GUI)) != 0;
        if (!command)
            return EditorKeyboardShortcut.None;

        bool shift = (modifiers & SDL_Keymod.SDL_KMOD_SHIFT) != 0;
        return key switch
        {
            SDL_Keycode.SDLK_O => EditorKeyboardShortcut.Open,
            SDL_Keycode.SDLK_S when shift => EditorKeyboardShortcut.SaveAs,
            SDL_Keycode.SDLK_S => EditorKeyboardShortcut.Save,
            SDL_Keycode.SDLK_Z when shift => EditorKeyboardShortcut.Redo,
            SDL_Keycode.SDLK_Z => EditorKeyboardShortcut.Undo,
            _ => EditorKeyboardShortcut.None,
        };
    }
}
