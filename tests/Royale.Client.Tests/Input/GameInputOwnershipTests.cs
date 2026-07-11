using Royale.Client.Input;
using Royale.Platform.Input;
using Royale.Rendering.UI;

namespace Royale.Client.Tests.Input;

public sealed class GameInputOwnershipTests
{
    [Fact]
    public void FreeCursorAndKeyboardCaptureSuppressesGameKeys()
    {
        var ownership = new GameInputOwnership(
            RelativeMouseModeEnabled: false,
            new ImGuiCaptureState(WantCaptureKeyboard: true, WantCaptureMouse: false));

        Assert.False(ownership.ShouldApplyKeyboardToGame(isGlobalControl: false));
    }

    [Fact]
    public void FreeCursorAndMouseCaptureSuppressesGameMouse()
    {
        var ownership = new GameInputOwnership(
            RelativeMouseModeEnabled: false,
            new ImGuiCaptureState(WantCaptureKeyboard: false, WantCaptureMouse: true));

        Assert.False(ownership.ShouldApplyMouseToGame());
    }

    [Fact]
    public void RelativeMouseModeIgnoresImguiCapture()
    {
        var ownership = new GameInputOwnership(
            RelativeMouseModeEnabled: true,
            new ImGuiCaptureState(WantCaptureKeyboard: true, WantCaptureMouse: true));

        Assert.True(ownership.ShouldApplyKeyboardToGame(isGlobalControl: false));
        Assert.True(ownership.ShouldApplyMouseToGame());
    }

    [Fact]
    public void GlobalControlsAreNeverSuppressed()
    {
        var ownership = new GameInputOwnership(
            RelativeMouseModeEnabled: false,
            new ImGuiCaptureState(WantCaptureKeyboard: true, WantCaptureMouse: true));

        Assert.True(ownership.ShouldApplyKeyboardToGame(isGlobalControl: true));
    }
}
