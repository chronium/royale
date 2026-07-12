using System.Text;
using System.Text.Json;

namespace Royale.Editor.Projects;

public sealed class RecentProjectStore
{
    private readonly string path;

    public RecentProjectStore(string? path = null)
    {
        this.path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Royale",
            "Editor",
            "recent-project.json");
    }

    public string? Read()
    {
        if (!File.Exists(path))
            return null;
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<State>(stream)?.ProjectPath;
    }

    public void Write(string projectPath)
    {
        string fullPath = Path.GetFullPath(projectPath);
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            string json = JsonSerializer.Serialize(new State(fullPath), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporary, json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n", new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private sealed record State(string ProjectPath);
}
