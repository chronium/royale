namespace Royale.Editor.Viewport;

public static class EditorMouseWheel
{
    public static float Normalize(float delta, bool flipped) => flipped ? -delta : delta;
}
