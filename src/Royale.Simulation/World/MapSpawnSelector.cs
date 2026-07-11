using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;

namespace Royale.Simulation.World;

public static class MapSpawnSelector
{
    public static bool TrySelectSpawn(
        GameMap map,
        MapStaticCollisionWorld collisionWorld,
        IEnumerable<SpawnReservation> reservations,
        out MapSpawnPoint? spawnPoint)
    {
        ArgumentNullException.ThrowIfNull(map);
        return TrySelectSpawn(map, map.SpawnPoints, collisionWorld, reservations, SpawnSelectionSettings.Default, out spawnPoint);
    }

    public static bool TrySelectSpawn(
        GameMap map,
        MapStaticCollisionWorld collisionWorld,
        IEnumerable<SpawnReservation> reservations,
        SpawnSelectionSettings settings,
        out MapSpawnPoint? spawnPoint)
    {
        ArgumentNullException.ThrowIfNull(map);
        return TrySelectSpawn(map, map.SpawnPoints, collisionWorld, reservations, settings, out spawnPoint);
    }

    public static bool TrySelectSpawn(
        GameMap map,
        IEnumerable<MapSpawnPoint> orderedCandidates,
        MapStaticCollisionWorld collisionWorld,
        IEnumerable<SpawnReservation> reservations,
        out MapSpawnPoint? spawnPoint) =>
        TrySelectSpawn(map, orderedCandidates, collisionWorld, reservations, SpawnSelectionSettings.Default, out spawnPoint);

    public static bool TrySelectSpawn(
        GameMap map,
        IEnumerable<MapSpawnPoint> orderedCandidates,
        MapStaticCollisionWorld collisionWorld,
        IEnumerable<SpawnReservation> reservations,
        SpawnSelectionSettings settings,
        out MapSpawnPoint? spawnPoint)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(orderedCandidates);
        ArgumentNullException.ThrowIfNull(collisionWorld);
        ArgumentNullException.ThrowIfNull(reservations);
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);

        SpawnReservation[] reservationArray = reservations as SpawnReservation[] ?? reservations.ToArray();

        foreach (MapSpawnPoint candidate in orderedCandidates)
        {
            SpawnReservation candidateReservation = CreateReservation(candidate, settings);

            if (collisionWorld.OverlapAabb(candidateReservation.LowerBound, candidateReservation.UpperBound).Count > 0)
                continue;

            if (reservationArray.Any(reservation => reservation.Overlaps(candidateReservation)))
                continue;

            spawnPoint = candidate;
            return true;
        }

        spawnPoint = null;
        return false;
    }

    public static SpawnReservation CreateReservation(MapSpawnPoint spawnPoint) =>
        CreateReservation(spawnPoint, SpawnSelectionSettings.Default);

    public static SpawnReservation CreateReservation(MapSpawnPoint spawnPoint, SpawnSelectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(spawnPoint);
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);

        MapVector3 position = spawnPoint.Position;
        return new SpawnReservation(
            new MapVector3(
                position.X - settings.PlayerRadius,
                position.Y + settings.GroundClearance,
                position.Z - settings.PlayerRadius),
            new MapVector3(
                position.X + settings.PlayerRadius,
                position.Y + settings.GroundClearance + settings.PlayerHeight,
                position.Z + settings.PlayerRadius));
    }

    private static void Validate(SpawnSelectionSettings settings)
    {
        if (settings.PlayerRadius <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Player radius must be positive.");

        if (settings.PlayerHeight <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Player height must be positive.");

        if (settings.GroundClearance < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Ground clearance must be non-negative.");
    }
}
