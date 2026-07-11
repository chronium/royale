using Royale.Gameplay.Tests.Infrastructure;

using Royale.Gameplay.Tests.Scenarios;

namespace Royale.Gameplay.Tests.Runner;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class WattleScenarioRunnerTests
{
    public static IEnumerable<object[]> ScenarioFiles =>
        WattleScenarioRunner
            .DiscoverScenarios(WattleScenarioRunner.DefaultScenarioDirectory)
            .Select(path => new object[] { path });

    [Fact]
    public void DiscoverScenariosFindsCopiedWattleFilesInDefaultDirectory()
    {
        IReadOnlyList<string> scenarios = WattleScenarioRunner.DiscoverScenarios(
            WattleScenarioRunner.DefaultScenarioDirectory);

        Assert.NotEmpty(scenarios);
        Assert.All(scenarios, path => Assert.EndsWith(".wattle", path, StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultScenarioDirectoryCanBeOverriddenForLocalIteration()
    {
        string? previousValue = Environment.GetEnvironmentVariable(
            WattleScenarioRunner.ScenarioDirectoryEnvironmentVariable);
        string overrideDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable(
                WattleScenarioRunner.ScenarioDirectoryEnvironmentVariable,
                overrideDirectory);

            Assert.Equal(overrideDirectory, WattleScenarioRunner.DefaultScenarioDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                WattleScenarioRunner.ScenarioDirectoryEnvironmentVariable,
                previousValue);
        }
    }

    [Fact]
    public void ExecuteFileReportsScenarioPathOnScriptFailure()
    {
        string scenarioDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string scenarioPath = Path.Combine(scenarioDirectory, "failing-scenario.wattle");

        Directory.CreateDirectory(scenarioDirectory);
        File.WriteAllText(scenarioPath, "scenario.assert.equal(1, 2);");

        try
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => WattleScenarioRunner.ExecuteFile(scenarioPath));

            Assert.Contains("failing-scenario.wattle", ex.Message, StringComparison.Ordinal);
            Assert.Contains(scenarioPath, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(scenarioPath);
            Directory.Delete(scenarioDirectory);
        }
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public void DiscoveredScenarioExecutesSuccessfully(string scenarioPath)
    {
        WattleScenarioRunner.ExecuteFile(scenarioPath);
    }
}
