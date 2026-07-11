using Royale.Rendering.Cameras;
using Royale.Client.Rendering.Cameras;
using Royale.Simulation.Movement;

namespace Royale.Client.Tests.Rendering.Cameras;

public sealed class PlayerEyeHeightSmootherTests
{
    [Fact]
    public void ReachesCrouchedEyeHeightWithinConfiguredTransition()
    {
        var smoother = new PlayerEyeHeightSmoother();
        Assert.Equal(PlayerViewSettings.DefaultEyeHeight, smoother.Update(PlayerViewSettings.DefaultEyeHeight, 0.0));

        float halfway = smoother.Update(PlayerViewSettings.DefaultCrouchedEyeHeight, 0.075);
        float completed = smoother.Update(PlayerViewSettings.DefaultCrouchedEyeHeight, 0.075);

        Assert.Equal((PlayerViewSettings.DefaultEyeHeight + PlayerViewSettings.DefaultCrouchedEyeHeight) * 0.5f, halfway, precision: 4);
        Assert.Equal(PlayerViewSettings.DefaultCrouchedEyeHeight, completed, precision: 4);
    }

    [Fact]
    public void ResetSnapsNextPlayerToCurrentStance()
    {
        var smoother = new PlayerEyeHeightSmoother();
        smoother.Update(PlayerViewSettings.DefaultEyeHeight, 0.0);
        smoother.Update(PlayerViewSettings.DefaultCrouchedEyeHeight, 0.05);

        smoother.Reset();

        Assert.Equal(PlayerViewSettings.DefaultCrouchedEyeHeight, smoother.Update(PlayerViewSettings.DefaultCrouchedEyeHeight, 0.01));
    }
}
