using Royale.Content.Maps;
namespace Royale.Editor.Workspace;
public sealed record EditorMapSummary(string Id, int StaticBoxes, int StaticModels, int SpawnPoints, int LootPoints, int NavigationNodes)
{
    public static EditorMapSummary Create(GameMap map) => new(map.Id, map.StaticBoxes.Count, map.StaticModels.Count, map.SpawnPoints.Count, map.LootPoints.Count, map.Navigation.Waypoints.Count);
}
