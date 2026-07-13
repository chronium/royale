namespace Royale.Editor.Playtest;

public sealed record PlaytestProcessStartInfo(
    string Name,
    string FileName,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments);
