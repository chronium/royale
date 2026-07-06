using Royale.Client.Platform;

namespace Royale.Client.Tests;

public sealed class ClientCameraModeControllerTests
{
    [Fact]
    public void DefaultModeIsGameplay()
    {
        var controller = new ClientCameraModeController();

        Assert.Equal(ClientCameraMode.Gameplay, controller.Mode);
        Assert.False(controller.IsFreecam);
        Assert.True(controller.ShouldApplyGameplayFixedUpdate);
    }

    [Fact]
    public void ToggleSwitchesBetweenGameplayAndFreecam()
    {
        var controller = new ClientCameraModeController();

        controller.Toggle();

        Assert.Equal(ClientCameraMode.Freecam, controller.Mode);
        Assert.True(controller.IsFreecam);
        Assert.False(controller.ShouldApplyGameplayFixedUpdate);

        controller.Toggle();

        Assert.Equal(ClientCameraMode.Gameplay, controller.Mode);
        Assert.False(controller.IsFreecam);
        Assert.True(controller.ShouldApplyGameplayFixedUpdate);
    }

    [Fact]
    public void DeadPlayersDoNotReceiveGameplayFixedUpdates()
    {
        var controller = new ClientCameraModeController();

        Assert.True(controller.ShouldApplyGameplayFixedUpdateForPlayer(playerAlive: true));
        Assert.False(controller.ShouldApplyGameplayFixedUpdateForPlayer(playerAlive: false));
    }

    [Fact]
    public void LocalPlayerDeathSwitchesToFreecamAndRespawnSwitchesToGameplay()
    {
        var controller = new ClientCameraModeController();

        controller.HandleLocalPlayerAliveTransition(wasAlive: true, isAlive: false);

        Assert.Equal(ClientCameraMode.Freecam, controller.Mode);
        Assert.True(controller.IsFreecam);

        controller.HandleLocalPlayerAliveTransition(wasAlive: false, isAlive: true);

        Assert.Equal(ClientCameraMode.Gameplay, controller.Mode);
        Assert.False(controller.IsFreecam);
    }
}
