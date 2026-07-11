using System.Numerics;
using Royale.Content.Maps;
using Royale.Protocol.Framing;
using Royale.Protocol.Input;
using Royale.Server.Match;
using Royale.Server.Simulation;
using Royale.Simulation.World;

namespace Royale.Server.Bots;

public sealed class BotNavigationSystem
{
    private const int ProgressWindowTicks = 90;
    private const float MeaningfulProgress = 0.1f;
    private const int MaximumRecoveryCount = 3;

    private readonly string mapId;
    private readonly MapBounds worldBounds;
    private readonly MapNavigationGraph graph;
    private readonly Dictionary<ServerPlayerId, NavigationState> states = [];

    public BotNavigationSystem(string mapId, MapBounds worldBounds, MapNavigationGraph graph)
    {
        this.mapId = mapId;
        this.worldBounds = worldBounds;
        this.graph = graph;
    }

    public int StateCount => states.Count;

    public void Add(ServerPlayerId playerId) => states[playerId] = new NavigationState();

    public void Remove(ServerPlayerId playerId) => states.Remove(playerId);

    public bool TryAssignGoal(ServerPlayerId playerId, Vector3 goal)
    {
        if (!IsFiniteAndInBounds(goal) || !states.TryGetValue(playerId, out NavigationState? state))
            return false;

        state.Kind = GoalKind.Assigned;
        state.Goal = goal;
        state.Path = [];
        state.RouteIndex = 0;
        state.Arrived = false;
        ResetRecovery(state);
        return true;
    }

    public bool TryClearGoal(ServerPlayerId playerId)
    {
        if (!states.TryGetValue(playerId, out NavigationState? state))
            return false;

        state.Kind = GoalKind.None;
        state.Path = [];
        state.RouteIndex = 0;
        ResetRecovery(state);
        return true;
    }

    public bool TryGenerate(
        AuthoritativePlayerState bot,
        MatchPhase phase,
        ulong tick,
        out BotInputIntent intent)
    {
        intent = default;
        if (phase != MatchPhase.Playing || !bot.Health.Alive ||
            graph.Waypoints.Count == 0 ||
            !states.TryGetValue(bot.PlayerId, out NavigationState? state))
        {
            return false;
        }

        if (state.Kind == GoalKind.None)
            SelectPatrol(bot, state);
        if (state.Kind == GoalKind.Assigned && state.Arrived)
            return false;

        EnsurePath(bot.Character.Position, state);
        AdvanceArrivedTargets(bot.Character.Position, state);
        if (state.Arrived)
            return false;

        Vector3 target = CurrentTarget(state);
        UpdateRecovery(bot, state, target, tick);
        if (state.Kind == GoalKind.None || state.Arrived)
            return false;

        target = CurrentTarget(state);
        Vector2 delta = new(target.X - bot.Character.Position.X, target.Z - bot.Character.Position.Z);
        if (delta.LengthSquared() <= 0.000001f)
            return false;

        float yaw = MathF.Atan2(delta.X, delta.Y);
        intent = new BotInputIntent(new Vector2(0.0f, 1.0f), yaw, 0.0f, InputButtons.None);
        return true;
    }

    private void SelectPatrol(AuthoritativePlayerState bot, NavigationState state)
    {
        MapNavigationWaypoint current = graph.FindNearest(bot.Character.Position);
        IReadOnlyList<MapNavigationWaypoint> candidates = graph.Waypoints.Count > 1
            ? graph.Waypoints.Where(waypoint => waypoint.Id != current.Id).ToArray()
            : graph.Waypoints;
        uint hash = StableHash(mapId);
        hash = Mix(hash, bot.PlayerId.Value);
        hash = Mix(hash, state.PatrolGeneration++);
        MapNavigationWaypoint destination = candidates[(int)(hash % (uint)candidates.Count)];
        state.Kind = GoalKind.Patrol;
        state.Goal = ToVector3(destination.Position);
        state.Arrived = false;
        state.Path = [];
        state.RouteIndex = 0;
        ResetRecovery(state);
    }

    private void EnsurePath(Vector3 position, NavigationState state)
    {
        if (state.Path.Count > 0)
            return;
        state.Path = graph.FindPath(position, state.Goal);
        state.RouteIndex = 0;
        state.Arrived = false;
    }

    private void AdvanceArrivedTargets(Vector3 position, NavigationState state)
    {
        while (state.RouteIndex < state.Path.Count && HasArrived(position, ToVector3(state.Path[state.RouteIndex].Position)))
        {
            state.RouteIndex++;
            ResetRecovery(state);
        }

        if (state.RouteIndex >= state.Path.Count && HasArrived(position, state.Goal))
        {
            state.Arrived = true;
            ResetRecovery(state);
            if (state.Kind == GoalKind.Patrol)
                state.Kind = GoalKind.None;
        }
    }

    private void UpdateRecovery(AuthoritativePlayerState bot, NavigationState state, Vector3 target, ulong tick)
    {
        float distance = HorizontalDistance(bot.Character.Position, target);
        if (!state.WindowStartTick.HasValue)
        {
            state.WindowStartTick = tick;
            state.WindowStartDistance = distance;
            return;
        }

        if (state.WindowStartDistance - distance >= MeaningfulProgress)
        {
            state.RecoveryCount = 0;
            state.WindowStartTick = tick;
            state.WindowStartDistance = distance;
            return;
        }

        if (tick - state.WindowStartTick.Value < ProgressWindowTicks)
            return;

        state.RecoveryCount++;
        state.Path = graph.FindPath(bot.Character.Position, state.Goal);
        state.RouteIndex = 0;
        state.WindowStartTick = tick;
        state.WindowStartDistance = HorizontalDistance(bot.Character.Position, CurrentTarget(state));
        if (state.RecoveryCount < MaximumRecoveryCount)
            return;

        state.Kind = GoalKind.None;
        state.Path = [];
        state.RouteIndex = 0;
        state.Arrived = false;
        ResetRecovery(state);
    }

    private Vector3 CurrentTarget(NavigationState state) =>
        state.RouteIndex < state.Path.Count ? ToVector3(state.Path[state.RouteIndex].Position) : state.Goal;

    private bool IsFiniteAndInBounds(Vector3 goal) =>
        float.IsFinite(goal.X) && float.IsFinite(goal.Y) && float.IsFinite(goal.Z) &&
        goal.X >= worldBounds.Min.X && goal.X <= worldBounds.Max.X &&
        goal.Y >= worldBounds.Min.Y && goal.Y <= worldBounds.Max.Y &&
        goal.Z >= worldBounds.Min.Z && goal.Z <= worldBounds.Max.Z;

    private static bool HasArrived(Vector3 position, Vector3 target) =>
        HorizontalDistance(position, target) <= MapNavigationGraph.ArrivalHorizontalTolerance &&
        MathF.Abs(position.Y - target.Y) <= MapNavigationGraph.ArrivalVerticalTolerance;

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        float x = first.X - second.X;
        float z = first.Z - second.Z;
        return MathF.Sqrt((x * x) + (z * z));
    }

    private static void ResetRecovery(NavigationState state)
    {
        state.RecoveryCount = 0;
        state.WindowStartTick = null;
        state.WindowStartDistance = 0.0f;
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (char character in value)
            hash = (hash ^ character) * 16777619;
        return hash;
    }

    private static uint Mix(uint hash, uint value) => (hash ^ value) * 16777619;

    private static Vector3 ToVector3(MapVector3 value) => new(value.X, value.Y, value.Z);

    private enum GoalKind { None, Patrol, Assigned }

    private sealed class NavigationState
    {
        public GoalKind Kind { get; set; }
        public Vector3 Goal { get; set; }
        public IReadOnlyList<MapNavigationWaypoint> Path { get; set; } = [];
        public int RouteIndex { get; set; }
        public bool Arrived { get; set; }
        public uint PatrolGeneration { get; set; }
        public ulong? WindowStartTick { get; set; }
        public float WindowStartDistance { get; set; }
        public int RecoveryCount { get; set; }
    }
}
