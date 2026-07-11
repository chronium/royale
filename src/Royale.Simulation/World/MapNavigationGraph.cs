using System.Numerics;
using Royale.Content.Maps;
using Royale.Simulation.Movement;

namespace Royale.Simulation.World;

public sealed class MapNavigationGraph
{
    public const float ArrivalHorizontalTolerance = 0.35f;
    public const float ArrivalVerticalTolerance = 0.35f;

    private readonly Dictionary<string, MapNavigationWaypoint> waypointsById;
    private readonly Dictionary<string, IReadOnlyList<MapNavigationWaypoint>> neighborsById;

    private MapNavigationGraph(GameMap map)
    {
        Waypoints = map.Navigation.Waypoints.OrderBy(waypoint => waypoint.Id, StringComparer.Ordinal).ToArray();
        waypointsById = Waypoints.ToDictionary(waypoint => waypoint.Id, StringComparer.Ordinal);
        var neighborIds = Waypoints.ToDictionary(
            waypoint => waypoint.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (MapNavigationLink link in map.Navigation.Links)
        {
            neighborIds[link.From].Add(link.To);
            neighborIds[link.To].Add(link.From);
        }

        neighborsById = neighborIds.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MapNavigationWaypoint>)pair.Value
                .Order(StringComparer.Ordinal)
                .Select(id => waypointsById[id])
                .ToArray(),
            StringComparer.Ordinal);
    }

    public IReadOnlyList<MapNavigationWaypoint> Waypoints { get; }

    public static MapNavigationGraph Create(GameMap map, MapStaticCollisionWorld collisionWorld)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(collisionWorld);

        var graph = new MapNavigationGraph(map);
        graph.ValidatePhysicalPlacement(map.Id, collisionWorld);
        graph.ValidatePhysicalLinks(map, collisionWorld);
        return graph;
    }

    public MapNavigationWaypoint GetWaypoint(string id)
    {
        if (!waypointsById.TryGetValue(id, out MapNavigationWaypoint? waypoint))
            throw new KeyNotFoundException($"Navigation waypoint '{id}' does not exist.");
        return waypoint;
    }

    public IReadOnlyList<MapNavigationWaypoint> GetNeighbors(string waypointId)
    {
        if (!neighborsById.TryGetValue(waypointId, out IReadOnlyList<MapNavigationWaypoint>? neighbors))
            throw new KeyNotFoundException($"Navigation waypoint '{waypointId}' does not exist.");
        return neighbors;
    }

    public MapNavigationWaypoint FindNearest(Vector3 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            throw new ArgumentOutOfRangeException(nameof(position), "Nearest-waypoint position must be finite.");

        return Waypoints
            .OrderBy(waypoint => Vector3.DistanceSquared(position, ToVector3(waypoint.Position)))
            .ThenBy(waypoint => waypoint.Id, StringComparer.Ordinal)
            .First();
    }

    private void ValidatePhysicalPlacement(string mapId, MapStaticCollisionWorld collisionWorld)
    {
        var controller = new KinematicCharacterController();
        KinematicCharacterSettings settings = controller.Settings;

        foreach (MapNavigationWaypoint waypoint in Waypoints)
        {
            Vector3 feet = ToVector3(waypoint.Position);
            float minimumGroundNormalY = MathF.Cos(settings.SlopeLimitDegrees * MathF.PI / 180.0f);
            IReadOnlyList<MapStaticCollisionPlane> authoredPlanes = collisionWorld
                .CollectCapsuleCollisionPlanes(feet, settings.Radius, settings.StandingHeight);
            MapStaticCollisionPlane? authoredObstruction = authoredPlanes
                .Where(plane => plane.Normal.Y < minimumGroundNormalY)
                .Cast<MapStaticCollisionPlane?>()
                .FirstOrDefault();
            if (authoredObstruction.HasValue)
                throw new InvalidDataException($"Map '{mapId}' navigation waypoint '{waypoint.Id}' standing capsule is obstructed by '{authoredObstruction.Value.Collider?.ContentId ?? "static collision"}'.");

            HashSet<MapStaticCollider> supports = authoredPlanes
                .Where(plane => plane.Collider is not null && plane.Normal.Y >= minimumGroundNormalY)
                .Select(plane => plane.Collider!)
                .ToHashSet();
            MapStaticCollider? overlappingObstruction = collisionWorld.OverlapAabb(
                    new MapVector3(feet.X - settings.Radius, feet.Y + 0.05f, feet.Z - settings.Radius),
                    new MapVector3(feet.X + settings.Radius, feet.Y + settings.StandingHeight, feet.Z + settings.Radius))
                .FirstOrDefault(collider => !supports.Contains(collider));
            if (overlappingObstruction is not null)
                throw new InvalidDataException($"Map '{mapId}' navigation waypoint '{waypoint.Id}' standing capsule is obstructed by '{overlappingObstruction.ContentId}'.");

            KinematicCharacterStepResult settled = controller.Step(
                collisionWorld,
                new KinematicCharacterState(feet, Vector3.Zero, IsGrounded: false),
                new KinematicCharacterInput(Vector2.Zero, Jump: false),
                SimulationSettings.FixedDeltaSeconds);
            if (!settled.State.IsGrounded || settled.State.Stance != KinematicCharacterStance.Standing ||
                HorizontalDistance(settled.State.Position, feet) > ArrivalHorizontalTolerance ||
                MathF.Abs(settled.State.Position.Y - feet.Y) > ArrivalVerticalTolerance)
            {
                throw new InvalidDataException($"Map '{mapId}' navigation waypoint '{waypoint.Id}' does not support a clear grounded standing capsule.");
            }

            Vector3 settledFeet = settled.State.Position;
            MapStaticCollisionPlane? obstruction = collisionWorld
                .CollectCapsuleCollisionPlanes(settledFeet, settings.Radius, settings.StandingHeight)
                .Where(plane => plane.Normal.Y < minimumGroundNormalY)
                .Cast<MapStaticCollisionPlane?>()
                .FirstOrDefault();
            if (obstruction.HasValue)
                throw new InvalidDataException($"Map '{mapId}' navigation waypoint '{waypoint.Id}' standing capsule is obstructed by '{obstruction.Value.Collider?.ContentId ?? "static collision"}'.");
        }
    }

    private void ValidatePhysicalLinks(GameMap map, MapStaticCollisionWorld collisionWorld)
    {
        foreach (MapNavigationLink link in map.Navigation.Links)
        {
            ValidateTraversal(map.Id, link, link.From, link.To, collisionWorld);
            ValidateTraversal(map.Id, link, link.To, link.From, collisionWorld);
        }
    }

    private void ValidateTraversal(
        string mapId,
        MapNavigationLink link,
        string fromId,
        string toId,
        MapStaticCollisionWorld collisionWorld)
    {
        var controller = new KinematicCharacterController();
        Vector3 start = ToVector3(waypointsById[fromId].Position);
        Vector3 target = ToVector3(waypointsById[toId].Position);
        float linkLength = Vector3.Distance(start, target);
        int tickBudget = checked((int)MathF.Ceiling(linkLength / controller.Settings.StandingSpeed * SimulationSettings.TickRateHz) + (2 * SimulationSettings.TickRateHz));
        KinematicCharacterState state = new(start, Vector3.Zero, IsGrounded: false);

        for (int tick = 0; tick < tickBudget; tick++)
        {
            Vector2 delta = new(target.X - state.Position.X, target.Z - state.Position.Z);
            Vector2 move = delta.LengthSquared() > 0.0f ? Vector2.Normalize(delta) : Vector2.Zero;
            state = controller.Step(
                collisionWorld,
                state,
                new KinematicCharacterInput(move, Jump: false, Crouch: false, Sprint: false),
                SimulationSettings.FixedDeltaSeconds).State;

            if (state.IsGrounded &&
                HorizontalDistance(state.Position, target) <= ArrivalHorizontalTolerance &&
                MathF.Abs(state.Position.Y - target.Y) <= ArrivalVerticalTolerance)
            {
                return;
            }
        }

        throw new InvalidDataException(
            $"Map '{mapId}' navigation link '{link.From}'-'{link.To}' is not physically traversable from waypoint '{fromId}' to '{toId}' by standing walk; stopped at ({state.Position.X:0.###}, {state.Position.Y:0.###}, {state.Position.Z:0.###}), grounded={state.IsGrounded}.");
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        float x = first.X - second.X;
        float z = first.Z - second.Z;
        return MathF.Sqrt((x * x) + (z * z));
    }

    private static Vector3 ToVector3(MapVector3 value) => new(value.X, value.Y, value.Z);
}
