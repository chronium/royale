using Royale.Client.Platform;

namespace Royale.Client.Tests;

public sealed class ImGuiDebugOverlayStateTests
{
    [Fact]
    public void FormatsOverlayText()
    {
        var state = new ImGuiDebugOverlayState(
            DeltaSeconds: 1.0 / 60.0,
            FixedTicksThisFrame: 2,
            TotalFixedTicks: 42,
            MouseCaptured: true);

        Assert.Equal("Frame 16.67 ms (60 FPS)", state.FrameTimingText);
        Assert.Equal("Fixed ticks this frame: 2", state.FixedTicksText);
        Assert.Equal("Total fixed tick: 42", state.TotalFixedTickText);
        Assert.Equal("Mouse: captured", state.MouseCaptureText);
    }

    [Fact]
    public void ZeroDeltaReportsZeroFps()
    {
        var state = new ImGuiDebugOverlayState(
            DeltaSeconds: 0,
            FixedTicksThisFrame: 0,
            TotalFixedTicks: 0,
            MouseCaptured: false);

        Assert.Equal(0.0, state.FramesPerSecond);
        Assert.Equal("Frame 0.00 ms (0 FPS)", state.FrameTimingText);
        Assert.Equal("Mouse: free", state.MouseCaptureText);
    }
}
