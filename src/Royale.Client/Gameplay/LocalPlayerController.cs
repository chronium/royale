using System.Numerics;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Gameplay;

public sealed class LocalPlayerController : IDisposable
{
    private readonly MapStaticCollisionWorld collisionWorld;
    private readonly KinematicCharacterController characterController;
    private readonly WeaponFeedbackState weaponFeedback = new();
    private WeaponFireState weaponFireState;
    private ulong fixedTick;
    private bool disposed;

    private LocalPlayerController(
        GameMap map,
        MapSpawnPoint spawnPoint,
        MapStaticCollisionWorld collisionWorld,
        KinematicCharacterController characterController,
        KinematicCharacterState characterState,
        PlayerLookState lookState,
        PlayerLookSettings lookSettings,
        PlayerViewSettings viewSettings,
        WeaponDefinition weapon,
        TrainingDummy trainingDummy)
    {
        Map = map;
        SpawnPoint = spawnPoint;
        this.collisionWorld = collisionWorld;
        this.characterController = characterController;
        CharacterState = characterState;
        LookState = lookState;
        LookSettings = lookSettings;
        ViewSettings = viewSettings;
        Weapon = weapon;
        TrainingDummy = trainingDummy;
        Health = HealthState.DefaultPlayer;
        weaponFireState = WeaponFireState.Ready;
    }

    public GameMap Map { get; }

    public MapSpawnPoint SpawnPoint { get; }

    public MapStaticCollisionWorld CollisionWorld => collisionWorld;

    public KinematicCharacterState CharacterState { get; private set; }

    public PlayerLookState LookState { get; private set; }

    public PlayerLookSettings LookSettings { get; }

    public PlayerViewSettings ViewSettings { get; }

    public KinematicCharacterSettings CharacterSettings => characterController.Settings;

    public WeaponDefinition Weapon { get; }

    public TrainingDummy TrainingDummy { get; }

    public HealthState Health { get; private set; }

    public bool Alive => Health.Alive;

    public WeaponFireState WeaponFireState => weaponFireState;

    public WeaponFireStepResult LastFireResult { get; private set; }

    public HitscanHit? LastHitscanResult { get; private set; }

    public DamageResult? LastTrainingDummyDamageResult { get; private set; }

    public WeaponFeedbackState WeaponFeedback => weaponFeedback;

    public int TotalShotsFired { get; private set; }

    public Vector3 FeetPosition => CharacterState.Position;

    public bool IsGrounded => CharacterState.IsGrounded;

    public static LocalPlayerController Create(
        GameMap map,
        PlayerLookState? initialLookState = null,
        PlayerLookSettings? lookSettings = null,
        KinematicCharacterController? characterController = null,
        PlayerViewSettings? viewSettings = null,
        TrainingDummy? trainingDummy = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        try
        {
            if (!MapSpawnSelector.TrySelectSpawn(map, collisionWorld, [], out MapSpawnPoint? spawnPoint))
                throw new InvalidOperationException($"Map '{map.Id}' does not contain a valid local player spawn.");

            MapSpawnPoint selectedSpawn = spawnPoint!;
            Vector3 feetPosition = ToVector3(selectedSpawn.Position);
            return new LocalPlayerController(
                map,
                selectedSpawn,
                collisionWorld,
                characterController ?? new KinematicCharacterController(),
                new KinematicCharacterState(feetPosition, Vector3.Zero, IsGrounded: false),
                initialLookState ?? new PlayerLookState(0.0f, 0.0f),
                lookSettings ?? PlayerLookSettings.Default,
                viewSettings ?? PlayerViewSettings.Default,
                WeaponCatalog.DefaultRifle,
                trainingDummy ?? TrainingDummy.CreateDefault(feetPosition));
        }
        catch
        {
            collisionWorld.Dispose();
            throw;
        }
    }

    public void UpdateLook(PlayerInputSample input)
    {
        ThrowIfDisposed();

        if (!Alive)
            return;

        LookState = PlayerLookController.ApplyMouseDelta(LookState, input.LookDelta, LookSettings);
    }

    public KinematicCharacterStepResult FixedUpdate(PlayerInputSample input, double fixedDeltaSeconds)
    {
        ThrowIfDisposed();

        if (!double.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds), "Fixed timestep must be finite and positive.");

        if (!Alive)
        {
            LastFireResult = new WeaponFireStepResult(
                Fired: false,
                weaponFireState,
                WeaponFireController.ResolveFireIntervalTicks(Weapon));
            LastHitscanResult = null;
            LastTrainingDummyDamageResult = null;
            return new KinematicCharacterStepResult(
                CharacterState,
                Vector3.Zero,
                JumpAccepted: false,
                HitCeiling: false,
                Stepped: false,
                SlideIterations: 0);
        }

        LastFireResult = WeaponFireController.Step(Weapon, weaponFireState, input.Fire, fixedTick);
        weaponFireState = LastFireResult.State;

        if (LastFireResult.Fired)
        {
            HitscanRay ray = HitscanResolver.CreatePlayerRay(CharacterState, LookState, ViewSettings, Weapon);
            LastHitscanResult = HitscanResolver.Resolve(
                collisionWorld,
                ray,
                [TrainingDummy.Target]);
            LastTrainingDummyDamageResult = TrainingDummy.ApplyDamage(Weapon, LastHitscanResult.Value, fixedTick);
            weaponFeedback.EmitShot(ray, LastHitscanResult.Value, LastTrainingDummyDamageResult);
            TotalShotsFired++;
        }
        else
        {
            LastHitscanResult = null;
            LastTrainingDummyDamageResult = null;
        }

        fixedTick++;

        Vector2 worldMove = PlayerMovementIntent.ToWorldMovement(input.Move, LookState.YawRadians);
        bool sprint = PlayerMovementIntent.IsSprintEligible(input.Move, input.Sprint);
        KinematicCharacterStepResult result = characterController.Step(
            collisionWorld,
            CharacterState,
            new KinematicCharacterInput(worldMove, input.Jump, input.Crouch, sprint),
            (float)fixedDeltaSeconds);

        CharacterState = result.State;
        return result;
    }

    public HealthState DebugApplyDamage(int damage)
    {
        ThrowIfDisposed();

        Health = Health.ApplyDamage(damage);
        return Health;
    }

    public HealthState DebugKill()
    {
        ThrowIfDisposed();

        return DebugApplyDamage(Health.MaxHealth);
    }

    public void DebugRespawn()
    {
        ThrowIfDisposed();

        Health = HealthState.DefaultPlayer;
        CharacterState = new KinematicCharacterState(ToVector3(SpawnPoint.Position), Vector3.Zero, IsGrounded: false);
        LookState = new PlayerLookState(0.0f, 0.0f);
        weaponFireState = WeaponFireState.Ready;
        LastFireResult = default;
        LastHitscanResult = null;
        LastTrainingDummyDamageResult = null;
        TotalShotsFired = 0;
        weaponFeedback.Clear();
    }

    public RenderCamera ToRenderCamera() =>
        GameplayView.CreateRenderCamera(
            FeetPosition,
            LookState with { PitchRadians = LookState.PitchRadians + weaponFeedback.RecoilPitchRadians },
            ViewSettings);

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

    private static Vector3 ToVector3(MapVector3 vector) => new(vector.X, vector.Y, vector.Z);
}
