using Royale.Editor.Platform;
using SDL;

namespace Royale.Editor.Tests.Platform;

public sealed class EditorKeyboardShortcutTests
{
    [Theory]
    [InlineData(SDL_Keycode.SDLK_O, SDL_Keymod.SDL_KMOD_GUI, EditorKeyboardShortcut.Open)]
    [InlineData(SDL_Keycode.SDLK_S, SDL_Keymod.SDL_KMOD_CTRL, EditorKeyboardShortcut.Save)]
    [InlineData(SDL_Keycode.SDLK_S, SDL_Keymod.SDL_KMOD_GUI | SDL_Keymod.SDL_KMOD_SHIFT, EditorKeyboardShortcut.SaveAs)]
    [InlineData(SDL_Keycode.SDLK_Z, SDL_Keymod.SDL_KMOD_GUI, EditorKeyboardShortcut.Undo)]
    [InlineData(SDL_Keycode.SDLK_Z, SDL_Keymod.SDL_KMOD_CTRL | SDL_Keymod.SDL_KMOD_SHIFT, EditorKeyboardShortcut.Redo)]
    public void ResolvesGlobalDocumentShortcuts(
        SDL_Keycode key,
        SDL_Keymod modifiers,
        EditorKeyboardShortcut expected)
    {
        Assert.Equal(expected, EditorKeyboardShortcutResolver.Resolve(key, modifiers));
    }

    [Fact]
    public void IgnoresKeysWithoutCommandModifier()
    {
        Assert.Equal(
            EditorKeyboardShortcut.None,
            EditorKeyboardShortcutResolver.Resolve(SDL_Keycode.SDLK_Z, SDL_Keymod.SDL_KMOD_NONE));
    }
}
