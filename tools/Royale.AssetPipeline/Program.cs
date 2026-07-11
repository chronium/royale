using Royale.AssetPipeline.Processing;

try
{
    AssetPipelineOptions options = AssetPipelineOptions.Parse(args);
    AssetPipelineProcessor.Build(options.ManifestPath, options.SourceRoot, options.OutputRoot, options.Audience);
    return 0;
}
catch (Exception exception) when (exception is not OutOfMemoryException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

internal readonly record struct AssetPipelineOptions(
    string ManifestPath,
    string SourceRoot,
    string OutputRoot,
    AssetPipelineAudience Audience)
{
    public static AssetPipelineOptions Parse(string[] args)
    {
        string? manifest = null;
        string? sourceRoot = null;
        string? output = null;
        AssetPipelineAudience? audience = null;

        for (int index = 0; index < args.Length; index++)
        {
            string value = ReadValue(args, ref index);
            switch (args[index - 1])
            {
                case "--manifest":
                    manifest = value;
                    break;
                case "--source-root":
                    sourceRoot = value;
                    break;
                case "--output":
                    output = value;
                    break;
                case "--audience":
                    audience = value switch
                    {
                        "client" => AssetPipelineAudience.Client,
                        "server" => AssetPipelineAudience.Server,
                        _ => throw new ArgumentException("--audience must be 'client' or 'server'."),
                    };
                    break;
                default:
                    throw new ArgumentException($"Unknown asset pipeline argument '{args[index - 1]}'.");
            }
        }

        return new AssetPipelineOptions(
            manifest ?? throw new ArgumentException("--manifest is required."),
            sourceRoot ?? throw new ArgumentException("--source-root is required."),
            output ?? throw new ArgumentException("--output is required."),
            audience ?? throw new ArgumentException("--audience is required."));
    }

    private static string ReadValue(string[] args, ref int index)
    {
        string option = args[index];
        if (!option.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Unexpected asset pipeline argument '{option}'.");
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"{option} requires a value.");
        return args[index];
    }
}
