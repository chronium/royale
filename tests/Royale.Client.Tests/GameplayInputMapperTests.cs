using System.Numerics;
using Royale.Client.Platform;
using Royale.Simulation;
using SDL;

namespace Royale.Client.Tests;

public sealed class GameplayInputMapperTests
{
    [Fact]
    public void WasdMapsToLocalMovementIntent()
    {
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_W);
        input.SetKeyDown((int)SDL_Keycode.SDLK_D);

        PlayerInputSample sample = GameplayInputMapper.FromInputState(input, relativeMouseModeEnabled: false);

        Assert.Equal(new Vector2(1.0f, 1.0f), sample.Move);
    }

    [Fact]
    public void OpposingMovementKeysCancel()
    {
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_W);
        input.SetKeyDown((int)SDL_Keycode.SDLK_S);
        input.SetKeyDown((int)SDL_Keycode.SDLK_A);
        input.SetKeyDown((int)SDL_Keycode.SDLK_D);

        PlayerInputSample sample = GameplayInputMapper.FromInputState(input, relativeMouseModeEnabled: false);

        Assert.Equal(Vector2.Zero, sample.Move);
    }

    [Fact]
    public void SpaceMapsToJump()
    {
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_SPACE);

        PlayerInputSample sample = GameplayInputMapper.FromInputState(input, relativeMouseModeEnabled: false);

        Assert.True(sample.Jump);
    }

    [Fact]
    public void LeftMouseMapsToFire()
    {
        var input = new InputState();
        input.SetMouseButtonDown((int)SDLButton.SDL_BUTTON_LEFT);

        PlayerInputSample sample = GameplayInputMapper.FromInputState(input, relativeMouseModeEnabled: false);

        Assert.True(sample.Fire);
    }

    [Fact]
    public void NoLeftMouseMapsToNoFire()
    {
        var input = new InputState();

        PlayerInputSample sample = GameplayInputMapper.FromInputState(input, relativeMouseModeEnabled: false);

        Assert.False(sample.Fire);
    }

    [Fact]
    public void MouseLookIsIgnoredWhenRelativeMouseModeIsDisabled()
    {
        var input = new InputState();
        input.AddMouseDelta(12.0f, -6.0f);

        PlayerInputSample sample = GameplayInputMapper.FromInputState(input, relativeMouseModeEnabled: false);

        Assert.Equal(Vector2.Zero, sample.LookDelta);
    }

    [Fact]
    public void MouseLookIsIncludedWhenRelativeMouseModeIsEnabled()
    {
        var input = new InputState();
        input.AddMouseDelta(12.0f, -6.0f);

        PlayerInputSample sample = GameplayInputMapper.FromInputState(input, relativeMouseModeEnabled: true);

        Assert.Equal(new Vector2(12.0f, -6.0f), sample.LookDelta);
    }
}
