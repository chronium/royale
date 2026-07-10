using System.Globalization;
using System.Numerics;
using Royale.Client.Gameplay;
using Royale.Client.Presentation;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using Royale.Client.UI;
using Royale.Content;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class ImGuiDebugOverlayStateTests
{
    [Fact]
    public void FormatsOverlayText()
    {
        var state = new ImGuiDebugOverlayState(
            DeltaSeconds: 1.0 / 60.0,
            FixedTicksThisFrame: 2,
            TotalFixedTicks: 42);

        Assert.Equal("Frame 16.67 ms (60 FPS)", state.FrameTimingText);
        Assert.Equal("Fixed ticks this frame: 2", state.FixedTicksText);
        Assert.Equal("Total fixed tick: 42", state.TotalFixedTickText);
    }

    [Fact]
    public void ZeroDeltaReportsZeroFps()
    {
        var state = new ImGuiDebugOverlayState(
            DeltaSeconds: 0,
            FixedTicksThisFrame: 0,
            TotalFixedTicks: 0);

        Assert.Equal(0.0, state.FramesPerSecond);
        Assert.Equal("Frame 0.00 ms (0 FPS)", state.FrameTimingText);
    }

    [Fact]
    public void OfflineOverlayUsesLivePlayerAndHidesNetworkSections()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateFloorMap());

        ImGuiDebugOverlayState state = ImGuiDebugOverlayState.CreateOffline(
            deltaSeconds: 1.0 / 60.0,
            fixedTicksThisFrame: 1,
            totalFixedTicks: 12,
            CreateDefaultRendererState(),
            player,
            staticColliderCount: 1);

        Assert.Null(state.Server);
        Assert.Null(state.Network);
        Assert.Equal("offline", state.Connection!.Mode);
        Assert.True(state.Player!.Available);
        Assert.Equal("offline simulation", state.Player.Values!.Source);
        Assert.Equal(player.FeetPosition, state.Player.Values.Position);
        Assert.Equal("Weapon: rifle", state.Player.Values.WeaponText);
        Assert.Equal("Ammunition: not tracked offline (magazine capacity 30)", state.Player.Values.AmmunitionText);
        Assert.True(state.Physics!.CollisionWorldAvailable);
        Assert.Equal(1, state.Physics.StaticColliderCount);
        Assert.Null(state.Simulation.ServerTick);
        Assert.Null(state.Simulation.PendingInputCount);
        Assert.Equal(ClientCameraMode.Gameplay, state.Renderer!.ActiveCameraMode);
        Assert.Equal("Launch position override: none", state.Renderer.LaunchPositionText);
        Assert.Equal("Screenshot: disabled", state.Renderer.ScreenshotStateText);
    }

    [Fact]
    public void RendererTelemetryFormatsFreecamLaunchOverridesWithInvariantCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ro-RO");
            var renderer = new TelemetryRendererState(
                ClientCameraMode.Freecam,
                ClientCameraMode.Freecam,
                new Vector3(4.5f, 2.25f, -3.75f),
                new Vector3(1.75f, 0.7f, -1.35f),
                RenderViewMode.WorldAndDebug,
                true,
                "graybox",
                18,
                1,
                1,
                true,
                5,
                4,
                "/tmp/royale-freecam.bmp");

            Assert.Equal("Active camera: Freecam", renderer.ActiveCameraText);
            Assert.Equal("Launch position override: (4.50, 2.25, -3.75)", renderer.LaunchPositionText);
            Assert.Equal("Launch look-at override: (1.75, 0.70, -1.35)", renderer.LaunchLookAtText);
            Assert.Equal("Render view: WorldAndDebug", renderer.RenderViewModeText);
            Assert.Equal("Mouse: captured", renderer.MouseCaptureText);
            Assert.Equal("Screenshot: pending", renderer.ScreenshotStateText);
            Assert.Equal("Screenshot target frame: 5", renderer.ScreenshotTargetFrameText);
            Assert.Equal(4, renderer.CompletedFrames);
            Assert.Equal("Screenshot output: /tmp/royale-freecam.bmp", renderer.ScreenshotOutputPathText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void RendererTelemetryReportsMapContentAndDisabledScreenshot()
    {
        GameMap map = MapCatalog.LoadDefault();
        TelemetryRendererState renderer = CreateDefaultRendererState();

        Assert.Equal(map.Id, renderer.LoadedMapId);
        Assert.Equal(map.StaticBoxes.Count, renderer.StaticBoxCount);
        Assert.Equal(map.StaticModels.Count, renderer.StaticModelCount);
        Assert.Equal(1, renderer.LoadedModelAssetCount);
        Assert.False(renderer.ScreenshotEnabled);
        Assert.Equal("Screenshot target frame: none", renderer.ScreenshotTargetFrameText);
        Assert.Equal("Screenshot output: none", renderer.ScreenshotOutputPathText);
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

    [Fact]
    public void FormatsPlayerDiagnosticsText()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateFloorMap());
        player.DebugKill();

        PlayerDiagnosticsState state = PlayerDiagnosticsState.FromPlayer(player);

        Assert.Equal("Health: 0/100", state.HealthText);
        Assert.Equal("State: dead", state.AliveText);
    }

    [Fact]
    public void FormatsWeaponFeedbackDiagnosticsText()
    {
        using LocalPlayerController player = LocalPlayerController.Create(
            CreateFloorMap(),
            trainingDummy: new TrainingDummy(new Vector3(0.0f, 0.0f, -10.0f)));

        player.FixedUpdate(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero), 1.0 / 60.0);

        PlayerDiagnosticsState state = PlayerDiagnosticsState.FromPlayer(player);

        Assert.Equal("Last shot: target", state.LastShotText);
        Assert.Equal("Hit marker: active", state.HitMarkerText);
        Assert.Equal("Hit id: training-dummy", state.HitIdentityText);
        Assert.Equal("Damage: 25", state.DamageText);
        Assert.Equal("Feedback: 3.00s", state.FeedbackLifetimeText);
    }

    private static GameMap CreateFloorMap() => new()
    {
        Id = "test-map",
        Name = "Test Map",
        SpawnPoints =
        [
            new MapSpawnPoint
            {
                Id = "spawn-a",
                Position = new MapVector3(0.0f, 0.0f, 0.0f),
            },
        ],
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "floor",
                Position = new MapVector3(0.0f, -0.1f, 0.0f),
                Size = new MapVector3(20.0f, 0.2f, 20.0f),
            },
        ],
    };

    private static TelemetryRendererState CreateDefaultRendererState()
    {
        GameMap map = MapCatalog.LoadDefault();
        return new TelemetryRendererState(
            ClientCameraMode.Gameplay,
            ClientCameraMode.Gameplay,
            null,
            null,
            RenderViewMode.WorldAndDebug,
            false,
            map.Id,
            map.StaticBoxes.Count,
            map.StaticModels.Count,
            1,
            false,
            null,
            0,
            null);
    }
}
