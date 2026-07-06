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
    }

    [Fact]
    public void ToggleSwitchesBetweenGameplayAndFreecam()
    {
        var controller = new ClientCameraModeController();

        controller.Toggle();

        Assert.Equal(ClientCameraMode.Freecam, controller.Mode);
        Assert.True(controller.IsFreecam);

        controller.Toggle();

        Assert.Equal(ClientCameraMode.Gameplay, controller.Mode);
        Assert.False(controller.IsFreecam);
    }
}
