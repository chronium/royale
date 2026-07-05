using Royale.Client.Platform;

namespace Royale.Client.Tests;

public sealed class InputStateTests
{
    [Fact]
    public void TracksKeyTransitions()
    {
        var input = new InputState();

        input.SetKeyDown(42);

        Assert.True(input.IsKeyDown(42));
        Assert.True(input.WasKeyPressed(42));
        Assert.False(input.WasKeyReleased(42));

        input.BeginFrame();

        Assert.True(input.IsKeyDown(42));
        Assert.False(input.WasKeyPressed(42));
        Assert.False(input.WasKeyReleased(42));

        input.SetKeyUp(42);

        Assert.False(input.IsKeyDown(42));
        Assert.False(input.WasKeyPressed(42));
        Assert.True(input.WasKeyReleased(42));
    }

    [Fact]
    public void IgnoresRepeatedKeyDownUntilReleased()
    {
        var input = new InputState();

        input.SetKeyDown(42);
        input.BeginFrame();
        input.SetKeyDown(42);

        Assert.True(input.IsKeyDown(42));
        Assert.False(input.WasKeyPressed(42));
    }

    [Fact]
    public void TracksMouseButtonTransitions()
    {
        var input = new InputState();

        input.SetMouseButtonDown(1);

        Assert.True(input.IsMouseButtonDown(1));
        Assert.True(input.WasMouseButtonPressed(1));
        Assert.False(input.WasMouseButtonReleased(1));

        input.BeginFrame();

        Assert.True(input.IsMouseButtonDown(1));
        Assert.False(input.WasMouseButtonPressed(1));
        Assert.False(input.WasMouseButtonReleased(1));

        input.SetMouseButtonUp(1);

        Assert.False(input.IsMouseButtonDown(1));
        Assert.False(input.WasMouseButtonPressed(1));
        Assert.True(input.WasMouseButtonReleased(1));
    }

    [Fact]
    public void AccumulatesMouseDeltaUntilFrameReset()
    {
        var input = new InputState();

        input.AddMouseDelta(2.5f, -1.0f);
        input.AddMouseDelta(0.5f, 4.0f);

        Assert.Equal(3.0f, input.MouseDeltaX);
        Assert.Equal(3.0f, input.MouseDeltaY);

        input.BeginFrame();

        Assert.Equal(0, input.MouseDeltaX);
        Assert.Equal(0, input.MouseDeltaY);
    }
}
