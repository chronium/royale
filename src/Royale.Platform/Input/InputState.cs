namespace Royale.Platform.Input;

public sealed class InputState
{
    private readonly HashSet<int> downKeys = [];
    private readonly HashSet<int> pressedKeys = [];
    private readonly HashSet<int> releasedKeys = [];
    private readonly HashSet<int> downMouseButtons = [];
    private readonly HashSet<int> pressedMouseButtons = [];
    private readonly HashSet<int> releasedMouseButtons = [];

    public float MouseDeltaX { get; private set; }
    public float MouseDeltaY { get; private set; }

    public bool IsKeyDown(int key) => downKeys.Contains(key);

    public bool WasKeyPressed(int key) => pressedKeys.Contains(key);

    public bool WasKeyReleased(int key) => releasedKeys.Contains(key);

    public bool IsMouseButtonDown(int button) => downMouseButtons.Contains(button);

    public bool WasMouseButtonPressed(int button) => pressedMouseButtons.Contains(button);

    public bool WasMouseButtonReleased(int button) => releasedMouseButtons.Contains(button);

    public void SetKeyDown(int key)
    {
        if (downKeys.Add(key))
        {
            pressedKeys.Add(key);
            releasedKeys.Remove(key);
        }
    }

    public void SetKeyUp(int key)
    {
        if (downKeys.Remove(key))
        {
            releasedKeys.Add(key);
            pressedKeys.Remove(key);
        }
    }

    public void SetMouseButtonDown(int button)
    {
        if (downMouseButtons.Add(button))
        {
            pressedMouseButtons.Add(button);
            releasedMouseButtons.Remove(button);
        }
    }

    public void SetMouseButtonUp(int button)
    {
        if (downMouseButtons.Remove(button))
        {
            releasedMouseButtons.Add(button);
            pressedMouseButtons.Remove(button);
        }
    }

    public void AddMouseDelta(float x, float y)
    {
        MouseDeltaX += x;
        MouseDeltaY += y;
    }

    public void BeginFrame()
    {
        pressedKeys.Clear();
        releasedKeys.Clear();
        pressedMouseButtons.Clear();
        releasedMouseButtons.Clear();
        MouseDeltaX = 0;
        MouseDeltaY = 0;
    }
}
