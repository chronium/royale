namespace Royale.Editor.Projects;

public sealed record RoyaleProjectManifest
{
    public const int CurrentVersion = 1;

    public int Version { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Map { get; init; } = string.Empty;

    public string AssetManifest { get; init; } = string.Empty;
}
