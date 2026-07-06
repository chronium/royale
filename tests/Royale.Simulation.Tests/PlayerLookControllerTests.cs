using System.Numerics;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Simulation.Tests;

public sealed class PlayerLookControllerTests
{
    [Fact]
    public void MouseDeltaChangesYawAndPitch()
    {
        var state = new PlayerLookState(YawRadians: 0.25f, PitchRadians: -0.1f);
        var settings = new PlayerLookSettings(
            MinPitchRadians: -1.0f,
            MaxPitchRadians: 1.0f,
            MouseSensitivityRadiansPerPixel: 0.01f);

        PlayerLookState updated = PlayerLookController.ApplyMouseDelta(
            state,
            new Vector2(10.0f, -5.0f),
            settings);

        Assert.Equal(0.35f, updated.YawRadians, precision: 5);
        Assert.Equal(-0.05f, updated.PitchRadians, precision: 5);
    }

    [Fact]
    public void PitchClampsToConfiguredLimits()
    {
        var settings = new PlayerLookSettings(
            MinPitchRadians: -0.5f,
            MaxPitchRadians: 0.75f,
            MouseSensitivityRadiansPerPixel: 0.01f);

        PlayerLookState maxClamped = PlayerLookController.ApplyMouseDelta(
            new PlayerLookState(0.0f, 0.0f),
            new Vector2(0.0f, -1000.0f),
            settings);
        PlayerLookState minClamped = PlayerLookController.ApplyMouseDelta(
            new PlayerLookState(0.0f, 0.0f),
            new Vector2(0.0f, 1000.0f),
            settings);

        Assert.Equal(0.75f, maxClamped.PitchRadians, precision: 5);
        Assert.Equal(-0.5f, minClamped.PitchRadians, precision: 5);
    }

    [Theory]
    [InlineData(float.NaN, 0.0f)]
    [InlineData(float.PositiveInfinity, 0.0f)]
    [InlineData(0.0f, float.NegativeInfinity)]
    public void NonFiniteDeltasDoNotCorruptLookState(float deltaX, float deltaY)
    {
        var state = new PlayerLookState(YawRadians: 0.3f, PitchRadians: 0.2f);

        PlayerLookState updated = PlayerLookController.ApplyMouseDelta(state, new Vector2(deltaX, deltaY));

        Assert.Equal(state, updated);
        Assert.True(float.IsFinite(updated.YawRadians));
        Assert.True(float.IsFinite(updated.PitchRadians));
    }
}
