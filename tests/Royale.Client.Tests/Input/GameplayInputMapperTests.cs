using System.Numerics;
using Royale.Client.Input;
using Royale.Platform.Input;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;
using SDL;

namespace Royale.Client.Tests.Input;

public sealed class GameplayInputMapperTests
{
    [Fact]
    public void WasdMapsToLocalMovementIntent()
    {
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_W);
        input.SetKeyDown((int)SDL_Keycode.SDLK_D);

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(input, relativeMouseModeEnabled: false);

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

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(input, relativeMouseModeEnabled: false);

        Assert.Equal(Vector2.Zero, sample.Move);
    }

    [Fact]
    public void SpaceMapsToJump()
    {
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_SPACE);

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(input, relativeMouseModeEnabled: false);

        Assert.True(sample.Jump);
    }

    [Fact]
    public void LeftMouseMapsToFire()
    {
        var input = new InputState();
        input.SetMouseButtonDown((int)SDLButton.SDL_BUTTON_LEFT);

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(input, relativeMouseModeEnabled: false);

        Assert.True(sample.Fire);
    }

    [Fact]
    public void NoLeftMouseMapsToNoFire()
    {
        var input = new InputState();

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(input, relativeMouseModeEnabled: false);

        Assert.False(sample.Fire);
    }

    [Fact]
    public void MouseLookIsIgnoredWhenRelativeMouseModeIsDisabled()
    {
        var input = new InputState();
        input.AddMouseDelta(12.0f, -6.0f);

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(input, relativeMouseModeEnabled: false);

        Assert.Equal(Vector2.Zero, sample.LookDelta);
    }

    [Fact]
    public void MouseLookIsIncludedWhenRelativeMouseModeIsEnabled()
    {
        var input = new InputState();
        input.AddMouseDelta(12.0f, -6.0f);

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(input, relativeMouseModeEnabled: true);

        Assert.Equal(new Vector2(12.0f, -6.0f), sample.LookDelta);
    }

    [Fact]
    public void CrouchTogglesOncePerOwnedKeyPress()
    {
        var mapper = new GameplayInputMapper();
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_C);

        Assert.True(mapper.FromInputState(input, false).Crouch);
        input.BeginFrame();
        Assert.True(mapper.FromInputState(input, false).Crouch);
        input.SetKeyUp((int)SDL_Keycode.SDLK_C);
        input.BeginFrame();
        input.SetKeyDown((int)SDL_Keycode.SDLK_C);
        Assert.False(mapper.FromInputState(input, false).Crouch);
    }

    [Fact]
    public void UnownedCrouchPressDoesNotToggleDesiredStance()
    {
        var mapper = new GameplayInputMapper();
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_C);

        PlayerInputSample sample = mapper.FromInputState(input, false, ownsGameplayInput: false);

        Assert.False(sample.Crouch);
        Assert.False(mapper.Crouched);
    }

    [Fact]
    public void LeftShiftMapsToHeldSprintAndReleases()
    {
        var mapper = new GameplayInputMapper();
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_LSHIFT);

        Assert.True(mapper.FromInputState(input, false).Sprint);

        input.SetKeyUp((int)SDL_Keycode.SDLK_LSHIFT);

        Assert.False(mapper.FromInputState(input, false).Sprint);
    }

    [Fact]
    public void UnownedGameplayInputClearsSprintIntent()
    {
        var input = new InputState();
        input.SetKeyDown((int)SDL_Keycode.SDLK_LSHIFT);

        PlayerInputSample sample = new GameplayInputMapper().FromInputState(
            input,
            relativeMouseModeEnabled: false,
            ownsGameplayInput: false);

        Assert.False(sample.Sprint);
    }
}
