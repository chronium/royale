using System.Globalization;
using Royale.Content;

namespace Royale.Editor.Launch;

public sealed record EditorLaunchOptions(string MapId, string? MapFilePath, string? ScreenshotPath, int ScreenshotAfterFrames, bool ResetLayout)
{
    public static EditorLaunchOptions Default { get; } = new(ContentCatalog.DefaultMapId, null, null, 0, false);
    public static EditorLaunchOptions Parse(IReadOnlyList<string> args)
    {
        string map = ContentCatalog.DefaultMapId; string? mapFile = null; string? screenshot = null; int frames = 0; bool reset = false;
        for (int i = 0; i < args.Count; i++) switch (args[i])
        {
            case "--map": map = Value(args, ref i, "--map"); break;
            case "--map-file": mapFile = Value(args, ref i, "--map-file"); break;
            case "--screenshot": screenshot = Value(args, ref i, "--screenshot"); break;
            case "--screenshot-after-frames": if (!int.TryParse(Value(args, ref i, "--screenshot-after-frames"), NumberStyles.None, CultureInfo.InvariantCulture, out frames) || frames < 1) throw new ArgumentException("--screenshot-after-frames must be a positive integer."); break;
            case "--reset-layout": reset = true; break;
            default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
        }
        if (string.IsNullOrWhiteSpace(map)) throw new ArgumentException("--map must be non-empty.");
        if (screenshot is not null && frames == 0) frames = 1;
        if (screenshot is null && frames != 0) throw new ArgumentException("--screenshot-after-frames requires --screenshot.");
        return new(map, mapFile, screenshot, frames, reset);
    }
    private static string Value(IReadOnlyList<string> args, ref int i, string option) { if (++i >= args.Count || string.IsNullOrWhiteSpace(args[i])) throw new ArgumentException($"{option} requires a value."); return args[i]; }
}
