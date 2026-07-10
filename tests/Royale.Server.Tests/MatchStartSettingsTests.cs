using Royale.Protocol;
using Royale.Server;

namespace Royale.Server.Tests;

public sealed class MatchStartSettingsTests
{
    [Fact]
    public void DefaultUsesTwoPlayersAndFiveSecondCountdown()
    {
        Assert.Equal(2, MatchStartSettings.Default.MinimumPlayers);
        Assert.Equal(300, MatchStartSettings.CountdownTicks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ProtocolConstants.MaxSnapshotPlayers)]
    public void ConstructorAcceptsInclusivePlayerBounds(int minimumPlayers)
    {
        Assert.Equal(minimumPlayers, new MatchStartSettings(minimumPlayers).MinimumPlayers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProtocolConstants.MaxSnapshotPlayers + 1)]
    public void ConstructorRejectsOutOfRangePlayerCounts(int minimumPlayers)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchStartSettings(minimumPlayers));
    }
}
