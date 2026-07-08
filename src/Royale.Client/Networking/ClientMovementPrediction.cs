using System.Numerics;
using Royale.Content;
using Royale.Protocol;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Networking;

internal sealed class ClientMovementPrediction : IDisposable
{
    private const int MaxPendingInputCount = 128;

    private readonly Func<string, GameMap> loadMap;
    private readonly KinematicCharacterController characterController = new();
    private readonly Queue<PlayerInputCommand> pendingInputs = [];
    private MapStaticCollisionWorld? collisionWorld;
    private KinematicCharacterState characterState;
    private PlayerSnapshotState predictedPlayer;
    private bool mapLoadAttempted;
    private bool seeded;
    private bool disposed;

    public ClientMovementPrediction(Func<string, GameMap> loadMap)
    {
        this.loadMap = loadMap ?? throw new ArgumentNullException(nameof(loadMap));
    }

    public bool MapAvailable => collisionWorld is not null;

    public bool Seeded => seeded;

    public bool Active => MapAvailable && seeded;

    public int PendingInputCount => pendingInputs.Count;

    public void EnsureMapLoaded(string mapId)
    {
        ThrowIfDisposed();

        if (mapLoadAttempted)
            return;

        mapLoadAttempted = true;

        try
        {
            GameMap map = loadMap(mapId);
            collisionWorld = MapStaticCollisionWorld.Create(map);
        }
        catch
        {
            collisionWorld?.Dispose();
            collisionWorld = null;
        }
    }

    public bool TryGetPredictedLocalPlayer(out PlayerSnapshotState player)
    {
        ThrowIfDisposed();

        if (!Active)
        {
            player = default;
            return false;
        }

        player = predictedPlayer;
        return true;
    }

    public void StoreSentInput(PlayerInputCommand command)
    {
        ThrowIfDisposed();

        if (pendingInputs.Count >= MaxPendingInputCount)
            pendingInputs.Dequeue();

        pendingInputs.Enqueue(command);
    }

    public void Step(PlayerInputCommand command)
    {
        ThrowIfDisposed();

        if (!Active || collisionWorld is null)
            return;

        Vector2 worldMove = PlayerMovementIntent.ToWorldMovement(command.Move, command.YawRadians);
        KinematicCharacterStepResult stepResult = characterController.Step(
            collisionWorld,
            characterState,
            new KinematicCharacterInput(worldMove, (command.Buttons & InputButtons.Jump) != 0),
            SimulationSettings.FixedDeltaSeconds);

        characterState = stepResult.State;
        predictedPlayer = predictedPlayer with
        {
            Position = characterState.Position,
            Velocity = characterState.Velocity,
            YawRadians = command.YawRadians,
            PitchRadians = command.PitchRadians,
        };
    }

    public void ApplySnapshot(ServerSnapshot snapshot)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(snapshot);

        DropAcknowledgedInputs(snapshot.AcknowledgedInputSequence);

        if (collisionWorld is null || !TryFindLocalPlayer(snapshot, out PlayerSnapshotState authoritativePlayer))
            return;

        if (!seeded || pendingInputs.Count == 0)
            SeedFrom(authoritativePlayer);
    }

    public void Reset()
    {
        ThrowIfDisposed();

        pendingInputs.Clear();
        collisionWorld?.Dispose();
        collisionWorld = null;
        mapLoadAttempted = false;
        seeded = false;
        characterState = default;
        predictedPlayer = default;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        collisionWorld?.Dispose();
        disposed = true;
    }

    private void DropAcknowledgedInputs(uint? acknowledgedInputSequence)
    {
        if (acknowledgedInputSequence is not uint acknowledged)
            return;

        while (pendingInputs.TryPeek(out PlayerInputCommand pending) && pending.Sequence <= acknowledged)
            pendingInputs.Dequeue();
    }

    private void SeedFrom(PlayerSnapshotState authoritativePlayer)
    {
        predictedPlayer = authoritativePlayer;
        characterState = new KinematicCharacterState(
            authoritativePlayer.Position,
            authoritativePlayer.Velocity,
            IsGrounded: false);
        seeded = true;
    }

    private static bool TryFindLocalPlayer(ServerSnapshot snapshot, out PlayerSnapshotState localPlayer)
    {
        if (snapshot.LocalPlayerId is not uint localPlayerId)
        {
            localPlayer = default;
            return false;
        }

        foreach (PlayerSnapshotState player in snapshot.Players)
        {
            if (player.PlayerId == localPlayerId)
            {
                localPlayer = player;
                return true;
            }
        }

        localPlayer = default;
        return false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
