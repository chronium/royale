using Royale.Content.Maps;
using Royale.Editor.Workspace;
namespace Royale.Editor.Tests.Workspace;
public sealed class WorkspaceTests
{
    [Fact] public void LayoutPathIsOutsideRepositoryWhenGivenUserDirectory() { string result=EditorLayoutPath.Resolve("/Users/test/Library/Application Support"); Assert.EndsWith(Path.Combine("Royale","Editor","imgui.ini"),result); Assert.DoesNotContain("/src/",result); }
    [Fact] public void VisibilityDefaultsOnAndResetIsConsumedOnce() { var state=new EditorWorkspaceState(); Assert.True(state.ViewportVisible&&state.HierarchyVisible&&state.LogVisible); state.RequestLayoutReset(); Assert.True(state.ConsumeLayoutReset()); Assert.False(state.ConsumeLayoutReset()); }
    [Fact] public void LogIsBounded() { var log=new EditorLog(2); log.Add("a");log.Add("b");log.Add("c"); Assert.Equal(["b","c"],log.Entries); }
    [Fact] public void SummaryCountsMapContent() { var map=new GameMap{Id="map",StaticBoxes=[new StaticBoxDefinition()],StaticModels=[new StaticModelDefinition()],SpawnPoints=[new MapSpawnPoint()],LootPoints=[new MapLootPoint()],Navigation=new MapNavigationDefinition{Waypoints=[new MapNavigationWaypoint()]}}; EditorMapSummary s=EditorMapSummary.Create(map); Assert.Equal(1,s.StaticBoxes);Assert.Equal(1,s.NavigationNodes); }
}
