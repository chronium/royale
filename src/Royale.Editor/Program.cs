using Microsoft.Extensions.Logging;
using Royale.Diagnostics.Logging;
using Royale.Editor.Launch;
using Royale.Editor.Platform;

using ILoggerFactory loggerFactory = RoyaleLogging.CreateConsoleLoggerFactory(LogLevel.Information);
ILogger logger = loggerFactory.CreateLogger("Royale.Editor");
try
{
    EditorLaunchOptions options = EditorLaunchOptions.Parse(args);
    logger.LogInformation("Editor startup beginning for map {MapId}.", options.MapId);
    using var application = new EditorApplication(options, loggerFactory.CreateLogger<EditorApplication>());
    application.Run();
}
catch (Exception exception)
{
    logger.LogCritical(exception, "Fatal editor startup error.");
    throw;
}
