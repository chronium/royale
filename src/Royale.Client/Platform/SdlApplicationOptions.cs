namespace Royale.Client.Platform;

public sealed record SdlApplicationOptions(string? ScreenshotPath, int ScreenshotAfterFrames)
{
    public static SdlApplicationOptions Default { get; } = new(null, 0);

    public static SdlApplicationOptions Parse(IReadOnlyList<string> args)
    {
        string? screenshotPath = null;
        int screenshotAfterFrames = 0;

        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--screenshot":
                    screenshotPath = ReadValue(args, ref index, "--screenshot");
                    break;

                case "--screenshot-after-frames":
                    string rawFrameCount = ReadValue(args, ref index, "--screenshot-after-frames");

                    if (!int.TryParse(rawFrameCount, out screenshotAfterFrames) || screenshotAfterFrames < 1)
                        throw new ArgumentException("--screenshot-after-frames must be a positive integer.");

                    break;
            }
        }

        if (screenshotPath is not null && screenshotAfterFrames == 0)
            screenshotAfterFrames = 1;

        if (screenshotPath is null && screenshotAfterFrames != 0)
            throw new ArgumentException("--screenshot-after-frames requires --screenshot.");

        return new SdlApplicationOptions(screenshotPath, screenshotAfterFrames);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
            throw new ArgumentException($"{optionName} requires a value.");

        index++;
        return args[index];
    }
}
