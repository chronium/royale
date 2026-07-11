using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

using Royale.Simulation.Tests.Infrastructure;

namespace Royale.Simulation.Tests.World;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class MapSpawnSelectorTests
{
    [Fact]
    public void SelectorChoosesFirstValidUnoccupiedSpawn()
    {
        GameMap map = CreateMap([
            Spawn("spawn-a", 0.0f),
            Spawn("spawn-b", 3.0f),
        ]);

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, [], out MapSpawnPoint? selected));
        Assert.Equal("spawn-a", selected!.Id);
    }

    [Fact]
    public void OrderedCandidatesAreEvaluatedInSuppliedOrder()
    {
        MapSpawnPoint spawnA = Spawn("spawn-a", 0.0f);
        MapSpawnPoint spawnB = Spawn("spawn-b", 3.0f);
        GameMap map = CreateMap([spawnA, spawnB]);

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.True(MapSpawnSelector.TrySelectSpawn(
            map,
            [spawnB, spawnA],
            collisionWorld,
            [],
            out MapSpawnPoint? selected));
        Assert.Equal("spawn-b", selected!.Id);
    }

    [Fact]
    public void OrderedCandidatesStillRejectStaticCollisionAndReservations()
    {
        MapSpawnPoint blocked = Spawn("blocked", 0.0f);
        MapSpawnPoint reserved = Spawn("reserved", 3.0f);
        MapSpawnPoint available = Spawn("available", 6.0f);
        GameMap map = CreateMap(
            [blocked, reserved, available],
            [
                Ground(),
                Box("blocker", new MapVector3(0.0f, 0.9f, 0.0f), new MapVector3(0.5f, 1.0f, 0.5f)),
            ]);

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);
        SpawnReservation reservation = MapSpawnSelector.CreateReservation(reserved);

        Assert.True(MapSpawnSelector.TrySelectSpawn(
            map,
            [blocked, reserved, available],
            collisionWorld,
            [reservation],
            out MapSpawnPoint? selected));
        Assert.Equal("available", selected!.Id);
    }

    [Fact]
    public void StaticCollisionOverlapRejectsSpawn()
    {
        GameMap map = CreateMap(
            [
                Spawn("spawn-a", 0.0f),
                Spawn("spawn-b", 3.0f),
            ],
            [
                Ground(),
                Box("spawn-a-blocker", new MapVector3(0.0f, 0.9f, 0.0f), new MapVector3(0.5f, 1.0f, 0.5f)),
            ]);

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, [], out MapSpawnPoint? selected));
        Assert.Equal("spawn-b", selected!.Id);
    }

    [Fact]
    public void ReservationOverlapRejectsSpawn()
    {
        MapSpawnPoint spawnA = Spawn("spawn-a", 0.0f);
        GameMap map = CreateMap([
            spawnA,
            Spawn("spawn-b", 3.0f),
        ]);

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);
        SpawnReservation reservation = MapSpawnSelector.CreateReservation(spawnA);

        Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, [reservation], out MapSpawnPoint? selected));
        Assert.Equal("spawn-b", selected!.Id);
    }

    [Fact]
    public void TouchingReservationDoesNotRejectSpawn()
    {
        MapSpawnPoint spawn = Spawn("spawn-a", 0.0f);
        GameMap map = CreateMap([spawn]);
        SpawnReservation spawnReservation = MapSpawnSelector.CreateReservation(spawn);
        SpawnReservation touchingReservation = spawnReservation with
        {
            LowerBound = new MapVector3(spawnReservation.UpperBound.X, spawnReservation.LowerBound.Y, spawnReservation.LowerBound.Z),
            UpperBound = new MapVector3(spawnReservation.UpperBound.X + 0.5f, spawnReservation.UpperBound.Y, spawnReservation.UpperBound.Z),
        };

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, [touchingReservation], out MapSpawnPoint? selected));
        Assert.Equal("spawn-a", selected!.Id);
    }

    [Fact]
    public void NonOverlappingReservationAllowsLaterValidSpawn()
    {
        GameMap map = CreateMap(
            [
                Spawn("spawn-a", 0.0f),
                Spawn("spawn-b", 3.0f),
            ],
            [
                Ground(),
                Box("spawn-a-blocker", new MapVector3(0.0f, 0.9f, 0.0f), new MapVector3(0.5f, 1.0f, 0.5f)),
            ]);
        SpawnReservation remoteReservation = new(
            new MapVector3(5.0f, 0.0f, 5.0f),
            new MapVector3(6.0f, 1.0f, 6.0f));

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, [remoteReservation], out MapSpawnPoint? selected));
        Assert.Equal("spawn-b", selected!.Id);
    }

    [Fact]
    public void AllBlockedCandidatesReturnFalse()
    {
        MapSpawnPoint spawnA = Spawn("spawn-a", 0.0f);
        MapSpawnPoint spawnB = Spawn("spawn-b", 3.0f);
        GameMap map = CreateMap([spawnA, spawnB]);
        SpawnReservation reservationA = MapSpawnSelector.CreateReservation(spawnA);
        SpawnReservation reservationB = MapSpawnSelector.CreateReservation(spawnB);

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.False(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, [reservationA, reservationB], out MapSpawnPoint? selected));
        Assert.Null(selected);
    }

    [Fact]
    public void SelectedSpawnReservationUsesFeetAnchorClearanceDimensions()
    {
        MapSpawnPoint spawn = new()
        {
            Id = "spawn-a",
            Position = new MapVector3(2.0f, 3.0f, 4.0f),
        };

        SpawnReservation reservation = MapSpawnSelector.CreateReservation(spawn);

        Assert.Equal(new MapVector3(1.65f, 3.05f, 3.65f), reservation.LowerBound);
        Assert.Equal(new MapVector3(2.35f, 4.85f, 4.35f), reservation.UpperBound);
    }

    [Fact]
    public void DefaultGrayboxCanReserveEverySpawnCandidate()
    {
        GameMap map = MapCatalog.LoadDefault();

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);
        var reservations = new List<SpawnReservation>();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < 12; index++)
        {
            Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, reservations, out MapSpawnPoint? selected));
            Assert.NotNull(selected);
            Assert.True(selectedIds.Add(selected.Id));

            SpawnReservation reservation = MapSpawnSelector.CreateReservation(selected);
            Assert.DoesNotContain(reservations, existing => existing.Overlaps(reservation));
            reservations.Add(reservation);
        }

        Assert.Equal(map.SpawnPoints.Count, selectedIds.Count);
        Assert.False(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, reservations, out MapSpawnPoint? exhausted));
        Assert.Null(exhausted);
    }

    [Fact]
    public void PrototypeArenaCanReserveEveryAuthoredSpawnCandidate()
    {
        GameMap map = MapCatalog.LoadById("prototype-arena");
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);
        var reservations = new List<SpawnReservation>();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < map.SpawnPoints.Count; index++)
        {
            Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, reservations, out MapSpawnPoint? selected));
            Assert.NotNull(selected);
            Assert.True(selectedIds.Add(selected.Id));
            SpawnReservation reservation = MapSpawnSelector.CreateReservation(selected);
            Assert.Empty(collisionWorld.OverlapAabb(reservation.LowerBound, reservation.UpperBound));
            reservations.Add(reservation);
        }

        Assert.Equal(12, selectedIds.Count);
        Assert.False(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, reservations, out MapSpawnPoint? exhausted));
        Assert.Null(exhausted);
    }

    [Fact]
    public void CourtyardCompoundCanReserveEightDistinctSpawns()
    {
        GameMap map = MapCatalog.LoadById("courtyard-compound");
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);
        var reservations = new List<SpawnReservation>();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < 8; index++)
        {
            Assert.True(MapSpawnSelector.TrySelectSpawn(map, collisionWorld, reservations, out MapSpawnPoint? selected),
                $"Could only reserve {index} courtyard spawns.");
            Assert.NotNull(selected);
            Assert.True(selectedIds.Add(selected.Id));
            reservations.Add(MapSpawnSelector.CreateReservation(selected));
        }

        Assert.Equal(8, selectedIds.Count);
    }

    private static GameMap CreateMap(IReadOnlyList<MapSpawnPoint> spawnPoints) =>
        CreateMap(spawnPoints, [Ground()]);

    private static GameMap CreateMap(IReadOnlyList<MapSpawnPoint> spawnPoints, IReadOnlyList<StaticBoxDefinition> staticBoxes) => new()
    {
        Id = "test-map",
        Name = "Test Map",
        WorldBounds = new MapBounds
        {
            Min = new MapVector3(-10.0f, -1.0f, -10.0f),
            Max = new MapVector3(10.0f, 5.0f, 10.0f),
        },
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, 0.0f),
            Radius = 8.0f,
        },
        SpawnPoints = spawnPoints.ToList(),
        StaticBoxes = staticBoxes.ToList(),
    };

    private static MapSpawnPoint Spawn(string id, float x) => new()
    {
        Id = id,
        Position = new MapVector3(x, 0.0f, 0.0f),
    };

    private static StaticBoxDefinition Ground() =>
        Box("ground", new MapVector3(0.0f, -0.1f, 0.0f), new MapVector3(20.0f, 0.2f, 20.0f));

    private static StaticBoxDefinition Box(string id, MapVector3 position, MapVector3 size) => new()
    {
        Id = id,
        Position = position,
        Size = size,
    };
}
