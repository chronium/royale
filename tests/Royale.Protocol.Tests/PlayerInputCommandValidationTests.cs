using System.Numerics;
using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class PlayerInputCommandValidationTests
{
    [Fact]
    public void ValidMovementLookAndButtonCommandPassesValidation()
    {
        var command = new PlayerInputCommand(
            Sequence: 7,
            ClientTick: 120,
            Move: new Vector2(0.6f, 0.8f),
            YawRadians: 1.25f,
            PitchRadians: 0.25f,
            Buttons: InputButtons.Jump);

        Assert.True(PlayerInputCommandValidation.IsValid(command));
    }

    [Fact]
    public void CombinedButtonFlagsPassValidation()
    {
        var command = ValidCommand() with
        {
            Buttons = InputButtons.Fire | InputButtons.Reload | InputButtons.Crouch | InputButtons.Sprint,
        };

        Assert.True(PlayerInputCommandValidation.IsValid(command));
    }

    [Fact]
    public void UndefinedButtonBitsFailValidation()
    {
        var command = ValidCommand() with
        {
            Buttons = (InputButtons)(1 << 15),
        };

        Assert.False(PlayerInputCommandValidation.IsValid(command));
    }

    [Theory]
    [InlineData(float.NaN, 0.0f, 0.0f, 0.0f)]
    [InlineData(float.PositiveInfinity, 0.0f, 0.0f, 0.0f)]
    [InlineData(0.0f, float.NegativeInfinity, 0.0f, 0.0f)]
    [InlineData(0.0f, 0.0f, float.NaN, 0.0f)]
    [InlineData(0.0f, 0.0f, 0.0f, float.PositiveInfinity)]
    public void NonFiniteMovementYawOrPitchFailsValidation(
        float moveX,
        float moveY,
        float yawRadians,
        float pitchRadians)
    {
        var command = ValidCommand() with
        {
            Move = new Vector2(moveX, moveY),
            YawRadians = yawRadians,
            PitchRadians = pitchRadians,
        };

        Assert.False(PlayerInputCommandValidation.IsValid(command));
    }

    [Fact]
    public void OverLengthMovementFailsValidation()
    {
        var command = ValidCommand() with
        {
            Move = new Vector2(1.0f + PlayerInputCommandValidation.MoveMagnitudeTolerance * 2.0f, 0.0f),
        };

        Assert.False(PlayerInputCommandValidation.IsValid(command));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void PitchOutsideValidRangeFailsValidation(int direction)
    {
        var command = ValidCommand() with
        {
            PitchRadians = direction < 0
                ? PlayerInputCommandValidation.MinPitchRadians - 0.001f
                : PlayerInputCommandValidation.MaxPitchRadians + 0.001f,
        };

        Assert.False(PlayerInputCommandValidation.IsValid(command));
    }

    private static PlayerInputCommand ValidCommand() => new(
        Sequence: 1,
        ClientTick: 2,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);
}
