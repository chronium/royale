using Royale.Platform.Desktop;

namespace Royale.Platform.Tests.Desktop;

public sealed class SdlDesktopHostTests
{
    [Fact]
    public void ExitRequestsAreIdempotentWithoutNativeInitialization()
    {
        using var host = CreateHost();

        host.RequestExit();
        host.RequestExit();

        Assert.True(host.IsExitRequested);
        Assert.False(host.IsInitialized);
    }

    [Fact]
    public void DisposalIsIdempotentWithoutNativeInitialization()
    {
        SdlDesktopHost host = CreateHost();

        host.Dispose();
        host.Dispose();

        Assert.False(host.IsInitialized);
        Assert.Null(host.Window);
    }

    private static SdlDesktopHost CreateHost() => new(
        new SdlWindowSettings("Test", 640, 480, default),
        new SdlLoopSettings(1.0 / 60.0, maximumCatchUpTicks: 4, idleDelayMilliseconds: 1));
}
