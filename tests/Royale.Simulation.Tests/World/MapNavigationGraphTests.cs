using System.Numerics;
using Royale.Content.Maps;
using Royale.Simulation.World;
using Royale.Simulation.Tests.Infrastructure;

namespace Royale.Simulation.Tests.World;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class MapNavigationGraphTests
{
    [Fact]
    public void GraphOrdersWaypointsNeighborsAndNearestTiesOrdinally()
    {
        GameMap map = CreateMap(
            [Waypoint("zeta", 1.0f, 0.0f), Waypoint("alpha", -1.0f, 0.0f), Waypoint("middle", 0.0f, 1.0f)],
            [Link("zeta", "middle"), Link("middle", "alpha")]);
        using MapStaticCollisionWorld world = MapStaticCollisionWorld.Create(map);

        MapNavigationGraph graph = MapNavigationGraph.Create(map, world);

        Assert.Equal(new[] { "alpha", "middle", "zeta" }, graph.Waypoints.Select(waypoint => waypoint.Id));
        Assert.Equal(new[] { "alpha", "zeta" }, graph.GetNeighbors("middle").Select(waypoint => waypoint.Id));
        Assert.Equal("alpha", graph.FindNearest(Vector3.Zero).Id);
        Assert.Same(graph.GetWaypoint("middle"), graph.FindNearest(new Vector3(0.0f, 0.0f, 0.9f)));
    }

    [Fact]
    public void WeightedAStarSelectsShortestRouteAndBreaksEqualCostTiesOrdinally()
    {
        GameMap map = CreateMap(
            [
                Waypoint("start", -3.0f, 0.0f),
                Waypoint("alpha", -1.0f, 1.0f),
                Waypoint("zeta", -1.0f, -1.0f),
                Waypoint("goal", 1.0f, 0.0f),
                Waypoint("detour", 0.0f, 4.0f),
            ],
            [
                Link("start", "zeta"), Link("zeta", "goal"),
                Link("start", "alpha"), Link("alpha", "goal"),
                Link("start", "detour"), Link("detour", "goal"),
            ]);
        using MapStaticCollisionWorld world = MapStaticCollisionWorld.Create(map);
        MapNavigationGraph graph = MapNavigationGraph.Create(map, world);

        IReadOnlyList<MapNavigationWaypoint> path = graph.FindPath(
            new Vector3(-3.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f));

        Assert.Equal(new[] { "start", "alpha", "goal" }, path.Select(waypoint => waypoint.Id));
    }

    [Fact]
    public void UnsupportedWaypointIsRejectedWithMapAndWaypointContext()
    {
        GameMap map = CreateMap(
            [Waypoint("grounded", 0.0f, 0.0f), Waypoint("unsupported", 7.0f, 0.0f)],
            [Link("grounded", "unsupported")]);
        using MapStaticCollisionWorld world = MapStaticCollisionWorld.Create(map);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MapNavigationGraph.Create(map, world));

        Assert.Contains("Map 'navigation-test'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("waypoint 'unsupported'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ObstructedStandingCapsuleIsRejected()
    {
        GameMap map = CreateMap(
            [Waypoint("clear", -2.0f, 0.0f), Waypoint("obstructed", 0.0f, 0.0f)],
            [Link("clear", "obstructed")],
            new StaticBoxDefinition
            {
                Id = "blocking-ceiling",
                Position = new MapVector3(0.0f, 1.6f, 0.0f),
                Size = new MapVector3(2.0f, 0.4f, 2.0f),
            });
        using MapStaticCollisionWorld world = MapStaticCollisionWorld.Create(map);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MapNavigationGraph.Create(map, world));

        Assert.Contains("waypoint 'obstructed'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockedUndirectedLinkIsRejected()
    {
        GameMap map = CreateMap(
            [Waypoint("left", -2.0f, 0.0f), Waypoint("right", 2.0f, 0.0f)],
            [Link("left", "right")],
            new StaticBoxDefinition
            {
                Id = "blocking-wall",
                Position = new MapVector3(0.0f, 1.0f, 0.0f),
                Size = new MapVector3(0.2f, 2.0f, 4.0f),
            });
        using MapStaticCollisionWorld world = MapStaticCollisionWorld.Create(map);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MapNavigationGraph.Create(map, world));

        Assert.Contains("link 'left'-'right'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("from waypoint", exception.Message, StringComparison.Ordinal);
    }

    private static GameMap CreateMap(
        List<MapNavigationWaypoint> waypoints,
        List<MapNavigationLink> links,
        StaticBoxDefinition? obstacle = null)
    {
        List<StaticBoxDefinition> boxes =
        [
            new StaticBoxDefinition
            {
                Id = "ground",
                Position = new MapVector3(0.0f, -0.1f, 0.0f),
                Size = new MapVector3(10.0f, 0.2f, 10.0f),
            },
        ];
        if (obstacle is not null)
            boxes.Add(obstacle);

        return new GameMap
        {
            Id = "navigation-test",
            Name = "Navigation Test",
            StaticBoxes = boxes,
            Navigation = new MapNavigationDefinition { Waypoints = waypoints, Links = links },
        };
    }

    private static MapNavigationWaypoint Waypoint(string id, float x, float z) =>
        new() { Id = id, Position = new MapVector3(x, 0.0f, z) };

    private static MapNavigationLink Link(string from, string to) => new() { From = from, To = to };
}
