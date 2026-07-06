using System.Numerics;
using Royale.Client.Rendering;
using Royale.Content;
using Royale.Simulation;

namespace Royale.Client.Gameplay;

public sealed class LocalPlayerController : IDisposable
{
    private readonly MapStaticCollisionWorld collisionWorld;
    private readonly KinematicCharacterController characterController;
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
        WeaponDefinition weapon)
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

    public WeaponFireState WeaponFireState => weaponFireState;

    public WeaponFireStepResult LastFireResult { get; private set; }

    public HitscanHit? LastHitscanResult { get; private set; }

    public int TotalShotsFired { get; private set; }

    public Vector3 FeetPosition => CharacterState.Position;

    public bool IsGrounded => CharacterState.IsGrounded;

    public static LocalPlayerController Create(
        GameMap map,
        PlayerLookState? initialLookState = null,
        PlayerLookSettings? lookSettings = null,
        KinematicCharacterController? characterController = null,
        PlayerViewSettings? viewSettings = null)
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
                WeaponCatalog.DefaultRifle);
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

        LookState = PlayerLookController.ApplyMouseDelta(LookState, input.LookDelta, LookSettings);
    }

    public KinematicCharacterStepResult FixedUpdate(PlayerInputSample input, double fixedDeltaSeconds)
    {
        ThrowIfDisposed();

        if (!double.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds), "Fixed timestep must be finite and positive.");

        LastFireResult = WeaponFireController.Step(Weapon, weaponFireState, input.Fire, fixedTick);
        weaponFireState = LastFireResult.State;

        LastHitscanResult = LastFireResult.Fired
            ? HitscanResolver.Resolve(
                collisionWorld,
                HitscanResolver.CreatePlayerRay(CharacterState, LookState, ViewSettings, Weapon))
            : null;

        if (LastFireResult.Fired)
            TotalShotsFired++;

        fixedTick++;

        Vector2 worldMove = ToWorldMovement(input.Move, LookState.YawRadians);
        KinematicCharacterStepResult result = characterController.Step(
            collisionWorld,
            CharacterState,
            new KinematicCharacterInput(worldMove, input.Jump),
            (float)fixedDeltaSeconds);

        CharacterState = result.State;
        return result;
    }

    public RenderCamera ToRenderCamera() =>
        GameplayView.CreateRenderCamera(FeetPosition, LookState, ViewSettings);

    public static Vector2 ToWorldMovement(Vector2 localMove, float yawRadians)
    {
        if (!float.IsFinite(localMove.X) || !float.IsFinite(localMove.Y) || !float.IsFinite(yawRadians))
            return Vector2.Zero;

        Vector3 forward = new(MathF.Sin(yawRadians), 0.0f, -MathF.Cos(yawRadians));
        Vector3 right = new(MathF.Cos(yawRadians), 0.0f, MathF.Sin(yawRadians));
        Vector3 worldMove = (right * localMove.X) + (forward * localMove.Y);
        return new Vector2(worldMove.X, worldMove.Z);
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

    private static Vector3 ToVector3(MapVector3 vector) => new(vector.X, vector.Y, vector.Z);
}
