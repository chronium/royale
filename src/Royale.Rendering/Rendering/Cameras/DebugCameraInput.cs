namespace Royale.Rendering.Cameras;

public readonly record struct DebugCameraInput(
    bool MoveForward,
    bool MoveBackward,
    bool MoveLeft,
    bool MoveRight,
    bool MoveUp,
    bool MoveDown,
    float MouseDeltaX,
    float MouseDeltaY,
    bool MouseLookEnabled);
