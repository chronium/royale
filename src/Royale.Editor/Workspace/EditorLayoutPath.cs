namespace Royale.Editor.Workspace;
public static class EditorLayoutPath
{
    public static string Resolve(string applicationDataDirectory) => Path.Combine(applicationDataDirectory, "Royale", "Editor", "imgui.ini");
    public static string Resolve() => Resolve(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
}
