namespace Royale.Client.Platform;

public sealed class ClientCameraModeController
{
    public ClientCameraMode Mode { get; private set; } = ClientCameraMode.Gameplay;

    public bool IsFreecam => Mode == ClientCameraMode.Freecam;

    public void Toggle()
    {
        Mode = Mode == ClientCameraMode.Gameplay
            ? ClientCameraMode.Freecam
            : ClientCameraMode.Gameplay;
    }
}
