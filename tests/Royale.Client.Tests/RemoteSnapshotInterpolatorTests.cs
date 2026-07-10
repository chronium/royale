using System.Numerics;
using Royale.Client.Networking;
using Royale.Client.Presentation;
using Royale.Protocol;

namespace Royale.Client.Tests;

public sealed class RemoteSnapshotInterpolatorTests
{
    [Fact]
    public void BracketingSnapshotsInterpolateRemoteTransform()
    {
        var interpolator = new RemoteSnapshotInterpolator();
        ServerSnapshot older = Snapshot(100, Remote(position: Vector3.Zero, velocity: Vector3.Zero));
        ServerSnapshot newer = Snapshot(
            106,
            Remote(
                position: new Vector3(6.0f, 0.0f, 0.0f),
                velocity: new Vector3(12.0f, 0.0f, 0.0f),
                yaw: 1.0f,
                pitch: 0.5f));
        interpolator.AddSnapshot(older);
        interpolator.AddSnapshot(newer);
        interpolator.Advance(0.15);

        ServerSnapshot presentation = interpolator.CreatePresentationSnapshot(newer)!;
        PlayerSnapshotState remote = Assert.Single(presentation.Players, player => player.PlayerId == 2);

        Assert.Equal(3.0f, remote.Position.X, precision: 4);
        Assert.Equal(6.0f, remote.Velocity.X, precision: 4);
        Assert.Equal(0.5f, remote.YawRadians, precision: 4);
        Assert.Equal(0.25f, remote.PitchRadians, precision: 4);
        Assert.True(interpolator.LastRenderUsedInterpolation);
        Assert.Equal(103.0, interpolator.LastInterpolationTargetTick, precision: 4);
    }

    [Fact]
    public void PresentationSnapshotCombinesInterpolatedRemoteAndPredictedLocalPlayer()
    {
        ClientNetworkState state = new();
        var interpolator = new RemoteSnapshotInterpolator();
        ServerSnapshot older = Snapshot(100, Remote(position: Vector3.Zero));
        ServerSnapshot newer = Snapshot(106, Remote(position: new Vector3(6.0f, 0.0f, 0.0f)));
        state.ApplySnapshot(newer);
        interpolator.AddSnapshot(older);
        interpolator.AddSnapshot(newer);
        interpolator.Advance(0.15);

        PlayerSnapshotState predictedLocal = newer.Players[0] with
        {
            Position = new Vector3(20.0f, 0.0f, -4.0f),
        };

        ServerSnapshot presentation = NetworkSnapshotPresentation.CreatePresentationSnapshot(
            state,
            predictedLocal,
            interpolator)!;

        PlayerSnapshotState local = Assert.Single(presentation.Players, player => player.PlayerId == 1);
        PlayerSnapshotState remote = Assert.Single(presentation.Players, player => player.PlayerId == 2);
        Assert.Equal(predictedLocal.Position, local.Position);
        Assert.Equal(3.0f, remote.Position.X, precision: 4);
        Assert.Equal(newer.Players[0].Position, state.LatestSnapshot!.Players[0].Position);
    }

    [Fact]
    public void LatestAuthoritativeSnapshotIsUsedUntilEnoughSamplesExist()
    {
        var interpolator = new RemoteSnapshotInterpolator();
        ServerSnapshot latest = Snapshot(100, Remote(position: new Vector3(5.0f, 0.0f, 0.0f)));
        interpolator.AddSnapshot(latest);
        interpolator.Advance(1.0);

        ServerSnapshot presentation = interpolator.CreatePresentationSnapshot(latest)!;

        Assert.Same(latest, presentation);
        Assert.False(interpolator.LastRenderUsedInterpolation);
        PlayerSnapshotState remote = Assert.Single(presentation.Players, player => player.PlayerId == 2);
        Assert.Equal(5.0f, remote.Position.X, precision: 4);
    }

    [Fact]
    public void MissingRemotePlayerInBracketFallsBackToNearestSample()
    {
        var interpolator = new RemoteSnapshotInterpolator();
        ServerSnapshot older = Snapshot(100, remotePlayer: null);
        ServerSnapshot newer = Snapshot(106, Remote(position: new Vector3(6.0f, 0.0f, 0.0f)));
        interpolator.AddSnapshot(older);
        interpolator.AddSnapshot(newer);
        interpolator.Advance(0.15);

        ServerSnapshot presentation = interpolator.CreatePresentationSnapshot(newer)!;
        PlayerSnapshotState remote = Assert.Single(presentation.Players, player => player.PlayerId == 2);

        Assert.Equal(6.0f, remote.Position.X, precision: 4);
        Assert.False(interpolator.LastRenderUsedInterpolation);
    }

    [Fact]
    public void TargetBeyondBufferedDataHoldsNearestSampleWithoutExtrapolation()
    {
        var interpolator = new RemoteSnapshotInterpolator();
        ServerSnapshot older = Snapshot(100, Remote(position: Vector3.Zero));
        ServerSnapshot newer = Snapshot(106, Remote(position: new Vector3(6.0f, 0.0f, 0.0f)));
        interpolator.AddSnapshot(older);
        interpolator.AddSnapshot(newer);
        interpolator.Advance(0.4);

        ServerSnapshot presentation = interpolator.CreatePresentationSnapshot(newer)!;
        PlayerSnapshotState remote = Assert.Single(presentation.Players, player => player.PlayerId == 2);

        Assert.Equal(6.0f, remote.Position.X, precision: 4);
        Assert.False(interpolator.LastRenderUsedInterpolation);
    }

    [Fact]
    public void YawInterpolationUsesShortestAngleAcrossWraparound()
    {
        var interpolator = new RemoteSnapshotInterpolator();
        float olderYaw = 170.0f * MathF.PI / 180.0f;
        float newerYaw = -170.0f * MathF.PI / 180.0f;
        ServerSnapshot older = Snapshot(100, Remote(position: Vector3.Zero, yaw: olderYaw));
        ServerSnapshot newer = Snapshot(106, Remote(position: Vector3.Zero, yaw: newerYaw));
        interpolator.AddSnapshot(older);
        interpolator.AddSnapshot(newer);
        interpolator.Advance(0.15);

        ServerSnapshot presentation = interpolator.CreatePresentationSnapshot(newer)!;
        PlayerSnapshotState remote = Assert.Single(presentation.Players, player => player.PlayerId == 2);

        Assert.True(MathF.Abs(MathF.Abs(remote.YawRadians) - MathF.PI) < 0.01f);
        Assert.True(interpolator.LastRenderUsedInterpolation);
    }

    [Fact]
    public void SnapshotHistoryIsBounded()
    {
        var interpolator = new RemoteSnapshotInterpolator(capacity: 3);

        for (ulong tick = 100; tick <= 112; tick += 3)
            interpolator.AddSnapshot(Snapshot(tick, Remote(position: new Vector3(tick, 0.0f, 0.0f))));

        Assert.Equal(3, interpolator.BufferedSnapshotCount);
    }

    private static PlayerSnapshotState Local() => new(
        PlayerId: 1,
        Position: new Vector3(-1.0f, 0.0f, 0.0f),
        Velocity: Vector3.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        CurrentHealth: 100,
        MaxHealth: 100,
        Alive: true,
        Weapon: Weapon());

    private static PlayerSnapshotState Remote(
        Vector3 position,
        Vector3? velocity = null,
        float yaw = 0.0f,
        float pitch = 0.0f) => new(
        PlayerId: 2,
        Position: position,
        Velocity: velocity ?? Vector3.Zero,
        YawRadians: yaw,
        PitchRadians: pitch,
        CurrentHealth: 100,
        MaxHealth: 100,
        Alive: true,
        Weapon: Weapon());

    private static WeaponSnapshotState Weapon() => new(
        "rifle",
        AmmoInMagazine: 30,
        ReserveAmmo: 90,
        NextAllowedFireTick: 0,
        LastFiredTick: null,
        IsReloading: false,
        ReloadCompleteTick: null);

    private static ServerSnapshot Snapshot(ulong serverTick, PlayerSnapshotState? remotePlayer) => new(
        ServerTick: serverTick,
        LocalPlayerId: 1,
        AcknowledgedInputSequence: null,
        Players: remotePlayer is PlayerSnapshotState remote
            ? [Local(), remote]
            : [Local()],
        Match: new MatchSnapshotState(
            ServerSnapshotMatchPhase.Playing,
            PhaseStartedTick: 60,
            LivingPlayerCount: remotePlayer is null ? 1 : 2,
            WinnerPlayerId: null),
        SafeZone: new SafeZoneSnapshotState(
            Center: Vector3.Zero,
            CurrentRadius: 100.0f,
            TargetRadius: 50.0f,
            LastUpdatedTick: 90));
}
