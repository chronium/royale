using System.Numerics;
using Royale.Client.Gameplay;
using Royale.Content;

namespace Royale.Client.Rendering;

public static class DebugSceneBuilder
{
    private static readonly Vector4 PlayerCapsuleColor = new(0.20f, 0.95f, 0.45f, 1.0f);
    private static readonly Vector4 TrainingDummyCapsuleColor = new(1.0f, 0.32f, 0.25f, 1.0f);
    private static readonly Vector4 SpawnColor = new(1.0f, 0.78f, 0.20f, 1.0f);
    private static readonly Vector4 SafeZoneColor = new(0.20f, 0.72f, 1.0f, 1.0f);

    public static DebugPrimitiveList Build(GameMap map, LocalPlayerController? localPlayer)
    {
        ArgumentNullException.ThrowIfNull(map);

        var primitives = new DebugPrimitiveList();

        if (localPlayer is not null)
        {
            Box3DDebugDrawAdapter.AppendWorld(localPlayer.CollisionWorld, primitives);
            AddLocalPlayerCapsule(primitives, localPlayer);
            AddTrainingDummyCapsule(primitives, localPlayer.TrainingDummy);
        }

        AddSpawnMarkers(primitives, map);
        AddSafeZoneBoundary(primitives, map);

        return primitives;
    }

    private static void AddTrainingDummyCapsule(DebugPrimitiveList primitives, TrainingDummy trainingDummy)
    {
        Vector3 feet = trainingDummy.FeetPosition;
        float radius = trainingDummy.Radius;
        float height = trainingDummy.Height;
        primitives.AddCapsule(
            feet + new Vector3(0.0f, radius, 0.0f),
            feet + new Vector3(0.0f, height - radius, 0.0f),
            radius,
            TrainingDummyCapsuleColor);
    }

    private static void AddLocalPlayerCapsule(DebugPrimitiveList primitives, LocalPlayerController localPlayer)
    {
        Vector3 feet = localPlayer.FeetPosition;
        float radius = localPlayer.CharacterSettings.Radius;
        float height = localPlayer.CharacterSettings.Height;
        primitives.AddCapsule(
            feet + new Vector3(0.0f, radius, 0.0f),
            feet + new Vector3(0.0f, height - radius, 0.0f),
            radius,
            PlayerCapsuleColor);
    }

    private static void AddSpawnMarkers(DebugPrimitiveList primitives, GameMap map)
    {
        foreach (MapSpawnPoint spawn in map.SpawnPoints)
        {
            Vector3 position = ToVector3(spawn.Position);
            primitives.AddPoint(position + new Vector3(0.0f, 0.08f, 0.0f), 0.25f, SpawnColor);
        }
    }

    private static void AddSafeZoneBoundary(DebugPrimitiveList primitives, GameMap map)
    {
        if (map.SafeZone.Radius <= 0.0f)
            return;

        primitives.AddCircleXz(ToVector3(map.SafeZone.Center), map.SafeZone.Radius, SafeZoneColor);
    }

    private static Vector3 ToVector3(MapVector3 vector) => new(vector.X, vector.Y, vector.Z);
}
