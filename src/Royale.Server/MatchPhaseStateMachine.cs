namespace Royale.Server;

public static class MatchPhaseStateMachine
{
    public static bool CanTransition(MatchPhase current, MatchPhase next) =>
        current switch
        {
            MatchPhase.WaitingForPlayers => next == MatchPhase.Countdown,
            MatchPhase.Countdown => next == MatchPhase.Playing,
            MatchPhase.Playing => next == MatchPhase.Finished,
            MatchPhase.Finished => next == MatchPhase.Resetting,
            MatchPhase.Resetting => next == MatchPhase.WaitingForPlayers,
            _ => false,
        };

    public static AuthoritativeMatchState Transition(
        AuthoritativeMatchState state,
        MatchPhase next,
        ulong transitionTick)
    {
        if (!Enum.IsDefined(state.Phase))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state.Phase,
                "Cannot transition from an unknown match phase.");
        }

        if (!Enum.IsDefined(next))
            throw new ArgumentOutOfRangeException(nameof(next), next, "Cannot transition to an unknown match phase.");

        if (transitionTick < state.PhaseStartedTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transitionTick),
                transitionTick,
                $"Transition tick cannot precede phase start tick '{state.PhaseStartedTick}'.");
        }

        if (!CanTransition(state.Phase, next))
            throw new InvalidOperationException($"Cannot transition match phase from '{state.Phase}' to '{next}'.");

        return state with
        {
            Phase = next,
            PhaseStartedTick = transitionTick,
        };
    }
}
