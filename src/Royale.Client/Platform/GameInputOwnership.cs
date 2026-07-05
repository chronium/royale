namespace Royale.Client.Platform;

public readonly record struct GameInputOwnership(bool RelativeMouseModeEnabled, ImGuiCaptureState ImGuiCapture)
{
    public bool ShouldApplyKeyboardToGame(bool isGlobalControl)
    {
        if (isGlobalControl || RelativeMouseModeEnabled)
            return true;

        return !ImGuiCapture.WantCaptureKeyboard;
    }

    public bool ShouldApplyMouseToGame()
    {
        if (RelativeMouseModeEnabled)
            return true;

        return !ImGuiCapture.WantCaptureMouse;
    }
}
