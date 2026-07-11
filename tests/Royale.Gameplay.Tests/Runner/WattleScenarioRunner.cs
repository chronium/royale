using WattleScript.Interpreter;

using Royale.Gameplay.Tests.Scenarios;

namespace Royale.Gameplay.Tests.Runner;

internal static class WattleScenarioRunner
{
    public const string ScenarioDirectoryEnvironmentVariable = "ROYALE_SCENARIO_DIR";

    public static string DefaultScenarioDirectory
    {
        get
        {
            string? overrideDirectory = Environment.GetEnvironmentVariable(ScenarioDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(overrideDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Scenarios")
                : overrideDirectory;
        }
    }

    public static IReadOnlyList<string> DiscoverScenarios(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Scenario directory is required.", nameof(directory));

        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Wattle scenario directory was not found: {directory}");

        return Directory
            .EnumerateFiles(directory, "*.wattle", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static DynValue ExecuteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Scenario file path is required.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Wattle scenario file was not found: {path}", path);

        string source = File.ReadAllText(path);
        string scenarioName = Path.GetFileName(path);

        try
        {
            return new WattleScenarioScriptHost().Execute(source);
        }
        catch (Exception ex) when (ex is not ArgumentException and not FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Wattle scenario '{scenarioName}' failed while executing '{path}'.",
                ex);
        }
    }
}
