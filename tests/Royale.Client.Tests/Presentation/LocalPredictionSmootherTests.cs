using System.Numerics;
using Royale.Client.Presentation;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

namespace Royale.Client.Tests.Presentation;

public sealed class LocalPredictionSmootherTests
{
    [Fact]
    public void FirstUpdateReturnsPredictedPlayer()
    {
        var smoother = new LocalPredictionSmoother();
        PlayerSnapshotState predicted = Player(position: new Vector3(1.0f, 0.0f, 2.0f));

        PlayerSnapshotState displayed = smoother.Update(
            predicted,
            reconciliationCount: 0,
            correctionDistance: 0.0f,
            deltaSeconds: 1.0 / 60.0);

        Assert.Equal(predicted, displayed);
    }

    [Fact]
    public void ReconciliationCorrectionPreservesDisplayedPositionThenDecays()
    {
        var smoother = new LocalPredictionSmoother();
        PlayerSnapshotState initial = Player(position: Vector3.Zero);
        PlayerSnapshotState corrected = Player(position: new Vector3(-0.3f, 0.0f, 0.0f));

        PlayerSnapshotState first = smoother.Update(
            initial,
            reconciliationCount: 1,
            correctionDistance: 0.0f,
            deltaSeconds: 1.0 / 60.0);
        PlayerSnapshotState preserved = smoother.Update(
            corrected,
            reconciliationCount: 2,
            correctionDistance: 0.3f,
            deltaSeconds: 1.0 / 60.0);
        PlayerSnapshotState decayed = smoother.Update(
            corrected,
            reconciliationCount: 2,
            correctionDistance: 0.3f,
            deltaSeconds: 1.0 / 60.0);

        Assert.Equal(Vector3.Zero, first.Position);
        Assert.Equal(Vector3.Zero, preserved.Position);
        Assert.InRange(decayed.Position.X, corrected.Position.X, preserved.Position.X);
        Assert.True(decayed.Position.X < preserved.Position.X - 0.001f);
    }

    [Fact]
    public void LargeCorrectionSnapsToPredictedPlayer()
    {
        var smoother = new LocalPredictionSmoother();

        _ = smoother.Update(
            Player(position: Vector3.Zero),
            reconciliationCount: 1,
            correctionDistance: 0.0f,
            deltaSeconds: 1.0 / 60.0);

        PlayerSnapshotState corrected = Player(position: new Vector3(4.0f, 0.0f, 0.0f));
        PlayerSnapshotState displayed = smoother.Update(
            corrected,
            reconciliationCount: 2,
            correctionDistance: 4.0f,
            deltaSeconds: 1.0 / 60.0);

        Assert.Equal(corrected.Position, displayed.Position);
    }

    [Fact]
    public void DeadPlayerSnapsAndClearsCorrectionOffset()
    {
        var smoother = new LocalPredictionSmoother();

        _ = smoother.Update(
            Player(position: Vector3.Zero),
            reconciliationCount: 1,
            correctionDistance: 0.0f,
            deltaSeconds: 1.0 / 60.0);
        _ = smoother.Update(
            Player(position: new Vector3(-0.3f, 0.0f, 0.0f)),
            reconciliationCount: 2,
            correctionDistance: 0.3f,
            deltaSeconds: 1.0 / 60.0);

        PlayerSnapshotState dead = Player(
            position: new Vector3(2.0f, 0.0f, 0.0f),
            alive: false);
        PlayerSnapshotState displayedDead = smoother.Update(
            dead,
            reconciliationCount: 3,
            correctionDistance: 2.0f,
            deltaSeconds: 1.0 / 60.0);
        PlayerSnapshotState respawned = Player(position: new Vector3(3.0f, 0.0f, 0.0f));
        PlayerSnapshotState displayedRespawned = smoother.Update(
            respawned,
            reconciliationCount: 4,
            correctionDistance: 0.0f,
            deltaSeconds: 1.0 / 60.0);

        Assert.Equal(dead.Position, displayedDead.Position);
        Assert.Equal(respawned.Position, displayedRespawned.Position);
    }

    private static PlayerSnapshotState Player(Vector3 position, bool alive = true) => new(
        PlayerId: 1,
        Kind: ServerSnapshotPlayerKind.Human,
        Position: position,
        Velocity: Vector3.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        CurrentHealth: alive ? 100 : 0,
        MaxHealth: 100,
        Alive: alive,
        Weapon: new WeaponSnapshotState(
            "rifle",
            AmmoInMagazine: 30,
            ReserveAmmo: 90,
            NextAllowedFireTick: 0,
            LastFiredTick: null,
            IsReloading: false,
            ReloadCompleteTick: null));
}
