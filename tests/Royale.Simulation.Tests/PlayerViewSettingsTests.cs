using Royale.Simulation;

namespace Royale.Simulation.Tests;

public sealed class PlayerViewSettingsTests
{
    [Fact]
    public void DefaultEyeHeightIsFirstPersonCameraHeight()
    {
        Assert.Equal(1.62f, PlayerViewSettings.Default.EyeHeight);
        Assert.Equal(1.62f, PlayerViewSettings.DefaultEyeHeight);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void EyeHeightMustBeFiniteAndPositive(float eyeHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerViewSettings(eyeHeight));
    }
}
