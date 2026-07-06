using Royale.Content;
using Royale.Simulation.Combat;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Server;

public sealed class HeadlessServerSimulation : IDisposable
{
    private readonly GameMap map;
    private readonly MapStaticCollisionWorld collisionWorld;
    private readonly Dictionary<ServerPlayerId, AuthoritativePlayerState> players = [];
    private readonly Dictionary<ServerPlayerId, SpawnReservation> spawnReservations = [];
    private uint nextPlayerId = 1;
    private bool disposed;

    private HeadlessServerSimulation(GameMap map, MapStaticCollisionWorld collisionWorld)
    {
        this.map = map;
        MapId = map.Id;
        this.collisionWorld = collisionWorld;
        SafeZoneState = CreateSafeZoneState(map);
        MatchState = new AuthoritativeMatchState(
            MatchPhase.WaitingForPlayers,
            PhaseStartedTick: 0,
            LivingPlayerCount: 0,
            WinnerPlayerId: null);
    }

    public ulong CurrentTick { get; private set; }

    public string MapId { get; }

    public int StaticColliderCount => collisionWorld.ColliderCount;

    public IReadOnlyDictionary<ServerPlayerId, AuthoritativePlayerState> Players => players;

    public AuthoritativeMatchState MatchState { get; private set; }

    public AuthoritativeSafeZoneState SafeZoneState { get; private set; }

    public bool IsDisposed => disposed;

    public static HeadlessServerSimulation Create(string mapId)
    {
        GameMap map = MapCatalog.LoadById(mapId);
        MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        try
        {
            return new HeadlessServerSimulation(map, collisionWorld);
        }
        catch
        {
            collisionWorld.Dispose();
            throw;
        }
    }

    public AuthoritativePlayerState AddPlayer(ServerConnectionId? connectionId = null)
    {
        ThrowIfDisposed();

        if (!MapSpawnSelector.TrySelectSpawn(map, collisionWorld, spawnReservations.Values, out MapSpawnPoint? spawnPoint))
            throw new InvalidOperationException($"Map '{MapId}' has no available unoccupied spawn point.");

        MapSpawnPoint selectedSpawn = spawnPoint
            ?? throw new InvalidOperationException($"Map '{MapId}' returned an invalid spawn selection.");
        ServerPlayerId playerId = new(nextPlayerId++);
        SpawnReservation spawnReservation = MapSpawnSelector.CreateReservation(selectedSpawn);
        AuthoritativePlayerState player = CreatePlayerState(playerId, connectionId, selectedSpawn, spawnReservation);

        players.Add(playerId, player);
        spawnReservations.Add(playerId, spawnReservation);
        RefreshLivingPlayerCount();

        return player;
    }

    public bool RemovePlayer(ServerPlayerId playerId)
    {
        ThrowIfDisposed();

        bool removed = players.Remove(playerId);
        spawnReservations.Remove(playerId);

        if (removed)
            RefreshLivingPlayerCount();

        return removed;
    }

    public bool TryGetPlayer(ServerPlayerId playerId, out AuthoritativePlayerState? player)
    {
        ThrowIfDisposed();

        return players.TryGetValue(playerId, out player);
    }

    public void Step()
    {
        ThrowIfDisposed();

        collisionWorld.Step(SimulationSettings.FixedDeltaSeconds, SimulationSettings.PhysicsSubStepCount);
        CurrentTick++;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        collisionWorld.Dispose();
        disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static AuthoritativePlayerState CreatePlayerState(
        ServerPlayerId playerId,
        ServerConnectionId? connectionId,
        MapSpawnPoint spawnPoint,
        SpawnReservation spawnReservation)
    {
        WeaponDefinition rifle = WeaponCatalog.DefaultRifle;

        return new AuthoritativePlayerState
        {
            PlayerId = playerId,
            ConnectionId = connectionId,
            Character = new KinematicCharacterState(
                MapStaticBoxTransforms.ToVector3(spawnPoint.Position),
                Velocity: default,
                IsGrounded: true),
            Look = new PlayerLookState(DegreesToRadians(spawnPoint.RotationEuler.Y), PitchRadians: 0.0f),
            Health = HealthState.DefaultPlayer,
            Weapon = new AuthoritativeWeaponState
            {
                WeaponId = rifle.Id,
                AmmoInMagazine = rifle.MagazineSize,
                ReserveAmmo = rifle.MagazineSize * 3,
                Fire = WeaponFireState.Ready,
                IsReloading = false,
                ReloadCompleteTick = null,
            },
            SpawnReservation = spawnReservation,
            LastProcessedInputSequence = null,
        };
    }

    private static AuthoritativeSafeZoneState CreateSafeZoneState(GameMap map) =>
        new(
            MapStaticBoxTransforms.ToVector3(map.SafeZone.Center),
            CurrentRadius: map.SafeZone.Radius,
            TargetRadius: map.SafeZone.Radius,
            LastUpdatedTick: 0);

    private void RefreshLivingPlayerCount()
    {
        int livingPlayerCount = players.Values.Count(player => player.Health.Alive);
        MatchState = MatchState with { LivingPlayerCount = livingPlayerCount };
    }

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
