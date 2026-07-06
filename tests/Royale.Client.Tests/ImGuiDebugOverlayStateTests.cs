using System.Numerics;
using Royale.Client.Gameplay;
using Royale.Client.Rendering;
using Royale.Client.Platform;
using Royale.Simulation;

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
            MouseCaptured: true,
            RenderViewMode: RenderViewMode.WorldAndDebug);

        Assert.Equal("Frame 16.67 ms (60 FPS)", state.FrameTimingText);
        Assert.Equal("Fixed ticks this frame: 2", state.FixedTicksText);
        Assert.Equal("Total fixed tick: 42", state.TotalFixedTickText);
        Assert.Equal("Mouse: captured", state.MouseCaptureText);
        Assert.Equal("Render view: WorldAndDebug", state.RenderViewModeText);
    }

    [Fact]
    public void ZeroDeltaReportsZeroFps()
    {
        var state = new ImGuiDebugOverlayState(
            DeltaSeconds: 0,
            FixedTicksThisFrame: 0,
            TotalFixedTicks: 0,
            MouseCaptured: false,
            RenderViewMode: RenderViewMode.Normal);

        Assert.Equal(0.0, state.FramesPerSecond);
        Assert.Equal("Frame 0.00 ms (0 FPS)", state.FrameTimingText);
        Assert.Equal("Mouse: free", state.MouseCaptureText);
    }

    [Fact]
    public void FormatsTrainingDummyDiagnosticsText()
    {
        var dummy = new TrainingDummy(new Vector3(0.0f, 0.0f, -10.0f));
        dummy.ApplyDamage(new DamageRequest("rifle", 25, new HitscanHit(
            HitscanHitType.Target,
            new Vector3(1.0f, 1.5f, -9.75f),
            Vector3.UnitZ,
            Distance: 9.75f,
            Fraction: 0.08f,
            StaticCollider: null,
            TrainingDummy.StableId)), tick: 42);

        TrainingDummyDiagnosticsState state = TrainingDummyDiagnosticsState.FromDummy(dummy);

        Assert.Equal("Health: 75/100", state.HealthText);
        Assert.Equal("State: alive", state.AliveText);
        Assert.Equal("Recent damage (1/16)", state.HistoryHeaderText);
        Assert.Equal(
            "tick 42: rifle raw 25 applied 25 hp 75 dist 9.75 hit (1.00, 1.50, -9.75) region - falloff - random -",
            TrainingDummyDiagnosticsState.FormatDamageEntry(state.DamageHistory[0]));
    }
}
