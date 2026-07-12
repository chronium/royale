namespace Royale.Editor.Viewport;

[Flags]
public enum EditorCameraActions
{
    None = 0,
    MoveForward = 1 << 0,
    MoveBackward = 1 << 1,
    MoveLeft = 1 << 2,
    MoveRight = 1 << 3,
    MoveUp = 1 << 4,
    MoveDown = 1 << 5,
    LeftBoost = 1 << 6,
    RightBoost = 1 << 7,
    Boost = LeftBoost | RightBoost,
}

public readonly record struct EditorCameraInput(
    EditorCameraActions Actions,
    float MouseDeltaX,
    float MouseDeltaY,
    float WheelY,
    bool ViewportHovered);
