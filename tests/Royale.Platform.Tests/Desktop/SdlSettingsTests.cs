using Royale.Platform.Desktop;
using SDL;

namespace Royale.Platform.Tests.Desktop;

public sealed class SdlSettingsTests
{
    [Fact]
    public void WindowSettingsRequireTitleAndPositiveLogicalSize()
    {
        Assert.Throws<ArgumentException>(() => new SdlWindowSettings("", 1280, 720, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SdlWindowSettings("Royale", 0, 720, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SdlWindowSettings("Royale", 1280, -1, default));
    }

    [Fact]
    public void WindowSettingsPreserveConfiguredValues()
    {
        const SDL_WindowFlags flags = SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        var settings = new SdlWindowSettings("Royale", 1280, 720, flags);

        Assert.Equal("Royale", settings.Title);
        Assert.Equal(1280, settings.Width);
        Assert.Equal(720, settings.Height);
        Assert.Equal(flags, settings.Flags);
    }

    [Theory]
    [InlineData(0.0, 4)]
    [InlineData(double.NaN, 4)]
    [InlineData(double.PositiveInfinity, 4)]
    [InlineData(1.0 / 60.0, 0)]
    public void LoopSettingsRequireFinitePositiveTiming(double delta, int catchUpTicks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SdlLoopSettings(delta, catchUpTicks, 1));
    }
}
