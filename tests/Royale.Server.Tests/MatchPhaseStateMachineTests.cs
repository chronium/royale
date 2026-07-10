using Royale.Server;

namespace Royale.Server.Tests;

public sealed class MatchPhaseStateMachineTests
{
    public static TheoryData<MatchPhase, MatchPhase> LegalTransitions => new()
    {
        { MatchPhase.WaitingForPlayers, MatchPhase.Countdown },
        { MatchPhase.Countdown, MatchPhase.Playing },
        { MatchPhase.Playing, MatchPhase.Finished },
        { MatchPhase.Finished, MatchPhase.Resetting },
        { MatchPhase.Resetting, MatchPhase.WaitingForPlayers },
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public void EveryLegalTransitionIsAcceptedAndStamped(
        MatchPhase current,
        MatchPhase next)
    {
        var winner = new ServerPlayerId(17);
        var state = new AuthoritativeMatchState(
            current,
            PhaseStartedTick: 10,
            LivingPlayerCount: 3,
            WinnerPlayerId: winner);

        Assert.True(MatchPhaseStateMachine.CanTransition(current, next));

        AuthoritativeMatchState transitioned = MatchPhaseStateMachine.Transition(state, next, transitionTick: 25);

        Assert.Equal(next, transitioned.Phase);
        Assert.Equal(25UL, transitioned.PhaseStartedTick);
        Assert.Equal(3, transitioned.LivingPlayerCount);
        Assert.Equal(winner, transitioned.WinnerPlayerId);
    }

    [Fact]
    public void CompleteCycleReturnsToWaitingForPlayers()
    {
        var state = new AuthoritativeMatchState(
            MatchPhase.WaitingForPlayers,
            PhaseStartedTick: 0,
            LivingPlayerCount: 0,
            WinnerPlayerId: null);

        state = MatchPhaseStateMachine.Transition(state, MatchPhase.Countdown, transitionTick: 1);
        state = MatchPhaseStateMachine.Transition(state, MatchPhase.Playing, transitionTick: 2);
        state = MatchPhaseStateMachine.Transition(state, MatchPhase.Finished, transitionTick: 3);
        state = MatchPhaseStateMachine.Transition(state, MatchPhase.Resetting, transitionTick: 4);
        state = MatchPhaseStateMachine.Transition(state, MatchPhase.WaitingForPlayers, transitionTick: 5);

        Assert.Equal(MatchPhase.WaitingForPlayers, state.Phase);
        Assert.Equal(5UL, state.PhaseStartedTick);
    }

    [Theory]
    [InlineData(MatchPhase.WaitingForPlayers, MatchPhase.WaitingForPlayers)]
    [InlineData(MatchPhase.WaitingForPlayers, MatchPhase.Playing)]
    [InlineData(MatchPhase.Playing, MatchPhase.Countdown)]
    [InlineData(MatchPhase.Resetting, MatchPhase.Finished)]
    public void SameSkippedAndReversedTransitionsAreRejected(
        MatchPhase current,
        MatchPhase next)
    {
        var state = new AuthoritativeMatchState(current, 0, 0, null);

        Assert.False(MatchPhaseStateMachine.CanTransition(current, next));
        Assert.Throws<InvalidOperationException>(
            () => MatchPhaseStateMachine.Transition(state, next, transitionTick: 0));
    }

    [Fact]
    public void UnknownPhasesAreRejected()
    {
        const MatchPhase unknown = (MatchPhase)byte.MaxValue;
        var validState = new AuthoritativeMatchState(MatchPhase.WaitingForPlayers, 0, 0, null);
        var unknownState = validState with { Phase = unknown };

        Assert.False(MatchPhaseStateMachine.CanTransition(unknown, MatchPhase.Countdown));
        Assert.False(MatchPhaseStateMachine.CanTransition(MatchPhase.WaitingForPlayers, unknown));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MatchPhaseStateMachine.Transition(unknownState, MatchPhase.Countdown, transitionTick: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MatchPhaseStateMachine.Transition(validState, unknown, transitionTick: 0));
    }

    [Fact]
    public void TimeRegressingTransitionIsRejected()
    {
        var state = new AuthoritativeMatchState(
            MatchPhase.WaitingForPlayers,
            PhaseStartedTick: 10,
            LivingPlayerCount: 0,
            WinnerPlayerId: null);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MatchPhaseStateMachine.Transition(state, MatchPhase.Countdown, transitionTick: 9));
    }
}
