using System.Numerics;
using Royale.Content;
using Royale.Protocol;
using Royale.Simulation.Combat;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Server;

public sealed class HeadlessServerSimulation : IDisposable
{
    private readonly GameMap map;
    private readonly MapStaticCollisionWorld collisionWorld;
    private readonly KinematicCharacterController characterController = new();
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
        return Create(map);
    }

    public static HeadlessServerSimulation Create(GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

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

    public void TransitionMatchPhase(MatchPhase nextPhase)
    {
        ThrowIfDisposed();
        MatchState = MatchPhaseStateMachine.Transition(MatchState, nextPhase, CurrentTick);
    }

    public void AcknowledgePlayerInputSequence(
        ServerPlayerId playerId,
        uint inputSequence,
        uint? clientTick = null)
    {
        ThrowIfDisposed();

        if (!players.TryGetValue(playerId, out AuthoritativePlayerState? player))
            throw new InvalidOperationException($"Cannot acknowledge input for unknown player '{playerId}'.");

        players[playerId] = player with
        {
            LastProcessedInputSequence = inputSequence,
            LastProcessedInputClientTick = clientTick,
        };
    }

    public ServerSnapshot CreateSnapshot(ServerPlayerId? recipientPlayerId = null)
    {
        ThrowIfDisposed();

        uint? localPlayerId = null;
        uint? acknowledgedInputSequence = null;

        if (recipientPlayerId is ServerPlayerId playerId)
        {
            if (!players.TryGetValue(playerId, out AuthoritativePlayerState? recipient))
                throw new InvalidOperationException($"Cannot create snapshot for unknown recipient player '{playerId}'.");

            localPlayerId = playerId.Value;
            acknowledgedInputSequence = recipient.LastProcessedInputSequence;
        }

        PlayerSnapshotState[] playerSnapshots = players.Values
            .OrderBy(player => player.PlayerId.Value)
            .Select(CreatePlayerSnapshot)
            .ToArray();

        return new ServerSnapshot(
            CurrentTick,
            localPlayerId,
            acknowledgedInputSequence,
            playerSnapshots,
            CreateMatchSnapshot(MatchState),
            CreateSafeZoneSnapshot(SafeZoneState));
    }

    public void Step()
    {
        Step(new Dictionary<ServerPlayerId, PlayerInputCommand>());
    }

    public void Step(IReadOnlyDictionary<ServerPlayerId, PlayerInputCommand> inputCommands)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(inputCommands);

        ValidateInputCommands(inputCommands);
        ApplyMovement(inputCommands);
        ApplyCombat(inputCommands);
        RefreshLivingPlayerCount();

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
            LastProcessedInputClientTick = null,
        };
    }

    private static PlayerSnapshotState CreatePlayerSnapshot(AuthoritativePlayerState player) =>
        new(
            player.PlayerId.Value,
            player.Character.Position,
            player.Character.Velocity,
            player.Look.YawRadians,
            player.Look.PitchRadians,
            player.Health.CurrentHealth,
            player.Health.MaxHealth,
            player.Health.Alive,
            CreateWeaponSnapshot(player.Weapon));

    private static WeaponSnapshotState CreateWeaponSnapshot(AuthoritativeWeaponState weapon) =>
        new(
            weapon.WeaponId,
            weapon.AmmoInMagazine,
            weapon.ReserveAmmo,
            weapon.Fire.NextAllowedFireTick,
            weapon.Fire.LastFiredTick,
            weapon.IsReloading,
            weapon.ReloadCompleteTick);

    private static MatchSnapshotState CreateMatchSnapshot(AuthoritativeMatchState match) =>
        new(
            MapMatchPhase(match.Phase),
            match.PhaseStartedTick,
            match.LivingPlayerCount,
            match.WinnerPlayerId?.Value);

    private static SafeZoneSnapshotState CreateSafeZoneSnapshot(AuthoritativeSafeZoneState safeZone) =>
        new(
            safeZone.Center,
            safeZone.CurrentRadius,
            safeZone.TargetRadius,
            safeZone.LastUpdatedTick);

    private static ServerSnapshotMatchPhase MapMatchPhase(MatchPhase phase) =>
        phase switch
        {
            MatchPhase.WaitingForPlayers => ServerSnapshotMatchPhase.WaitingForPlayers,
            MatchPhase.Playing => ServerSnapshotMatchPhase.Playing,
            MatchPhase.Finished => ServerSnapshotMatchPhase.Finished,
            MatchPhase.Countdown => ServerSnapshotMatchPhase.Countdown,
            MatchPhase.Resetting => ServerSnapshotMatchPhase.Resetting,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown match phase."),
        };

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

    private void ValidateInputCommands(IReadOnlyDictionary<ServerPlayerId, PlayerInputCommand> inputCommands)
    {
        foreach ((ServerPlayerId playerId, PlayerInputCommand command) in inputCommands)
        {
            if (!players.ContainsKey(playerId))
                throw new InvalidOperationException($"Cannot apply input for unknown player '{playerId}'.");

            if (!PlayerInputCommandValidation.IsValid(command))
                throw new ArgumentException($"Cannot apply invalid input command for player '{playerId}'.", nameof(inputCommands));
        }
    }

    private void ApplyMovement(IReadOnlyDictionary<ServerPlayerId, PlayerInputCommand> inputCommands)
    {
        foreach (ServerPlayerId playerId in players.Keys.OrderBy(id => id.Value).ToArray())
        {
            AuthoritativePlayerState player = players[playerId];
            bool hasCommand = inputCommands.TryGetValue(playerId, out PlayerInputCommand command);

            if (hasCommand)
            {
                player = player with
                {
                    LastProcessedInputSequence = command.Sequence,
                    LastProcessedInputClientTick = command.ClientTick,
                };
            }

            if (!player.Health.Alive)
            {
                players[playerId] = player;
                continue;
            }

            if (hasCommand)
            {
                player = player with
                {
                    Look = new PlayerLookState(command.YawRadians, command.PitchRadians),
                };
            }

            Vector2 localMove = hasCommand ? command.Move : Vector2.Zero;
            bool jump = hasCommand && (command.Buttons & InputButtons.Jump) != 0;
            Vector2 worldMove = PlayerMovementIntent.ToWorldMovement(localMove, player.Look.YawRadians);
            KinematicCharacterStepResult stepResult = characterController.Step(
                collisionWorld,
                player.Character,
                new KinematicCharacterInput(worldMove, jump),
                SimulationSettings.FixedDeltaSeconds);

            players[playerId] = player with
            {
                Character = stepResult.State,
            };
        }
    }

    private void ApplyCombat(IReadOnlyDictionary<ServerPlayerId, PlayerInputCommand> inputCommands)
    {
        foreach (ServerPlayerId shooterId in players.Keys.OrderBy(id => id.Value).ToArray())
        {
            if (!inputCommands.TryGetValue(shooterId, out PlayerInputCommand command) ||
                (command.Buttons & InputButtons.Fire) == 0)
            {
                continue;
            }

            AuthoritativePlayerState shooter = players[shooterId];
            if (!shooter.Health.Alive || shooter.Weapon.AmmoInMagazine <= 0)
                continue;

            WeaponDefinition weapon = WeaponCatalog.GetById(shooter.Weapon.WeaponId);
            WeaponFireStepResult fireResult = WeaponFireController.Step(
                weapon,
                shooter.Weapon.Fire,
                fireHeld: true,
                CurrentTick);

            if (!fireResult.Fired)
                continue;

            shooter = shooter with
            {
                Weapon = shooter.Weapon with
                {
                    AmmoInMagazine = shooter.Weapon.AmmoInMagazine - 1,
                    Fire = fireResult.State,
                },
            };
            players[shooterId] = shooter;

            HitscanRay ray = HitscanResolver.CreatePlayerRay(
                shooter.Character,
                shooter.Look,
                PlayerViewSettings.Default,
                weapon);
            HitscanHit hit = HitscanResolver.Resolve(collisionWorld, ray, CreateTargetCandidates(shooterId));
            ApplyDamage(weapon, hit, shooterId);
        }
    }

    private IEnumerable<HitscanTarget> CreateTargetCandidates(ServerPlayerId shooterId) =>
        players.Values
            .Where(player => player.PlayerId != shooterId && player.Health.Alive)
            .OrderBy(player => player.PlayerId.Value)
            .Select(player => HitscanTarget.FromCharacter(
                CreateTargetId(player.PlayerId),
                player.Character,
                characterController.Settings));

    private void ApplyDamage(WeaponDefinition weapon, HitscanHit hit, ServerPlayerId shooterId)
    {
        Dictionary<string, HealthState> targetHealth = players.Values
            .Where(player => player.PlayerId != shooterId && player.Health.Alive)
            .ToDictionary(
                player => CreateTargetId(player.PlayerId),
                player => player.Health,
                StringComparer.Ordinal);

        DamageResult damage = DamageController.Apply(weapon, hit, targetHealth);
        if (!damage.Applied || string.IsNullOrWhiteSpace(damage.TargetId))
            return;

        if (!TryParseTargetId(damage.TargetId, out ServerPlayerId targetId) ||
            !players.TryGetValue(targetId, out AuthoritativePlayerState? target) ||
            !targetHealth.TryGetValue(damage.TargetId, out HealthState health))
        {
            return;
        }

        players[targetId] = target with
        {
            Health = health,
        };
    }

    private static string CreateTargetId(ServerPlayerId playerId) =>
        playerId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool TryParseTargetId(string targetId, out ServerPlayerId playerId)
    {
        if (uint.TryParse(
            targetId,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out uint value))
        {
            playerId = new ServerPlayerId(value);
            return true;
        }

        playerId = default;
        return false;
    }

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
