using Royale.Client.Platform;

namespace Royale.Client.Tests;

public sealed class SdlApplicationTests
{
    [Fact]
    public void StartsInGameplayCameraMode()
    {
        using var application = new SdlApplication();

        Assert.Equal(ClientCameraMode.Gameplay, application.CameraMode);
    }
}
