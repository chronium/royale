namespace Royale.Client.Platform;

public sealed class ClientCameraModeController
{
    public ClientCameraMode Mode { get; private set; } = ClientCameraMode.Gameplay;

    public bool IsFreecam => Mode == ClientCameraMode.Freecam;

    public bool ShouldApplyGameplayFixedUpdate => Mode == ClientCameraMode.Gameplay;

    public bool ShouldApplyGameplayFixedUpdateForPlayer(bool playerAlive) =>
        playerAlive && ShouldApplyGameplayFixedUpdate;

    public void Toggle()
    {
        Mode = Mode == ClientCameraMode.Gameplay
            ? ClientCameraMode.Freecam
            : ClientCameraMode.Gameplay;
    }

    public void SwitchToFreecam()
    {
        Mode = ClientCameraMode.Freecam;
    }

    public void SwitchToGameplay()
    {
        Mode = ClientCameraMode.Gameplay;
    }

    public void HandleLocalPlayerAliveTransition(bool wasAlive, bool isAlive)
    {
        if (wasAlive && !isAlive)
        {
            SwitchToFreecam();
            return;
        }

        if (!wasAlive && isAlive)
            SwitchToGameplay();
    }
}
