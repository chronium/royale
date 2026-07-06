using System.Numerics;
using Royale.Client.Gameplay;
using Royale.Content;
using Royale.Simulation;

namespace Royale.Client.Rendering;

public static class DebugSceneBuilder
{
    private const float MuzzleForwardOffsetMeters = 0.35f;
    private const float MuzzleMarkerSizeMeters = 0.048f;
    private const float ImpactMarkerSizeMeters = 0.22f;

    private static readonly Vector4 PlayerCapsuleColor = new(0.20f, 0.95f, 0.45f, 1.0f);
    private static readonly Vector4 TrainingDummyCapsuleColor = new(1.0f, 0.32f, 0.25f, 1.0f);
    private static readonly Vector4 SpawnColor = new(1.0f, 0.78f, 0.20f, 1.0f);
    private static readonly Vector4 SafeZoneColor = new(0.20f, 0.72f, 1.0f, 1.0f);
    private static readonly Vector4 MuzzleColor = new(1.0f, 0.92f, 0.18f, 1.0f);
    private static readonly Vector4 TracerColor = new(1.0f, 0.55f, 0.08f, 1.0f);
    private static readonly Vector4 StaticImpactColor = new(1.0f, 0.18f, 0.10f, 1.0f);
    private static readonly Vector4 TargetImpactColor = new(0.62f, 1.0f, 0.70f, 1.0f);

    public static DebugPrimitiveList Build(GameMap map, LocalPlayerController? localPlayer)
    {
        ArgumentNullException.ThrowIfNull(map);

        var primitives = new DebugPrimitiveList();

        if (localPlayer is not null)
        {
            Box3DDebugDrawAdapter.AppendWorld(localPlayer.CollisionWorld, primitives);
            AddLocalPlayerCapsule(primitives, localPlayer);
            AddTrainingDummyCapsule(primitives, localPlayer.TrainingDummy);
            AddWeaponFeedback(primitives, localPlayer.WeaponFeedback.ActiveShot);
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

    private static void AddWeaponFeedback(DebugPrimitiveList primitives, WeaponFeedbackShot? activeShot)
    {
        if (activeShot is not WeaponFeedbackShot shot || !shot.Active)
            return;

        Vector3 muzzlePosition = shot.Origin + (shot.Direction * MuzzleForwardOffsetMeters);
        primitives.AddPoint(muzzlePosition, MuzzleMarkerSizeMeters, MuzzleColor);
        primitives.AddLine(muzzlePosition, shot.End, TracerColor);

        if (shot.HitType == HitscanHitType.Static)
            primitives.AddPoint(shot.End, ImpactMarkerSizeMeters, StaticImpactColor);
        else if (shot.HitType == HitscanHitType.Target)
            primitives.AddPoint(shot.End, ImpactMarkerSizeMeters, TargetImpactColor);
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
