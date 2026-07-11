using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Server.Bots;
using Royale.Server.Launch;
using Royale.Server.Match;
using Royale.Server.Networking;
using Royale.Server.Observability;
using Royale.Server.Sessions;
using Royale.Server.Simulation;

namespace Royale.Server.Tests.Match;

public sealed class MatchStartSettingsTests
{
    [Fact]
    public void DefaultUsesPlannedLobbyAndPreparationDurations()
    {
        Assert.Equal(2, MatchStartSettings.Default.MinimumPlayers);
        Assert.Equal(8, MatchStartSettings.Default.TargetPlayers);
        Assert.Equal(300, MatchStartSettings.Default.WaitingSeconds);
        Assert.Equal(120, MatchStartSettings.Default.PreparationSeconds);
        Assert.Equal(18_000, MatchStartSettings.Default.WaitingTicks);
        Assert.Equal(7_200, MatchStartSettings.Default.PreparationTicks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ProtocolConstants.MaxSnapshotPlayers)]
    public void ConstructorAcceptsInclusivePlayerBounds(int minimumPlayers)
    {
        Assert.Equal(
            minimumPlayers,
            new MatchStartSettings(minimumPlayers, targetPlayers: minimumPlayers).MinimumPlayers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProtocolConstants.MaxSnapshotPlayers + 1)]
    public void ConstructorRejectsOutOfRangePlayerCounts(int minimumPlayers)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchStartSettings(minimumPlayers));
    }

    [Fact]
    public void ConstructorRejectsMinimumAboveTarget()
    {
        Assert.Throws<ArgumentException>(() => new MatchStartSettings(minimumPlayers: 3, targetPlayers: 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProtocolConstants.MaxSnapshotPlayers + 1)]
    public void ConstructorRejectsOutOfRangeTargetCounts(int targetPlayers)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MatchStartSettings(targetPlayers: targetPlayers));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(35_791_395)]
    public void ConstructorRejectsInvalidOrOverflowingDurations(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MatchStartSettings(waitingSeconds: seconds));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MatchStartSettings(preparationSeconds: seconds));
    }
}
