using System.Text;
using Microsoft.Extensions.Logging;
using Royale.Diagnostics.Logging;
using Royale.Diagnostics.Telemetry;
using ZLogger;

namespace Royale.Diagnostics.Tests.Telemetry;

public sealed class RoyaleTelemetryTests
{
    [Fact]
    public void ServerTelemetryCanBeCreatedAndDisposedWithoutOtlpEnvironment()
    {
        using var stream = new NonClosingMemoryStream();
        RoyaleTelemetry telemetry = RoyaleTelemetry.CreateServerForTesting(
            stream,
            LogLevel.Information,
            new RoyaleTelemetryEnvironment(null, null, null));

        Assert.False(telemetry.OtlpExportEnabled);
        Assert.False(telemetry.HasTracerProvider);
        Assert.False(telemetry.HasMeterProvider);

        telemetry.Dispose();
    }

    [Fact]
    public void ServerTelemetryKeepsZLoggerPlainTextFormat()
    {
        using var stream = new NonClosingMemoryStream();
        RoyaleTelemetry telemetry = RoyaleTelemetry.CreateServerForTesting(
            stream,
            LogLevel.Trace,
            new RoyaleTelemetryEnvironment(null, null, null));
        ILogger logger = telemetry.LoggerFactory.CreateLogger("Royale.Tests.Telemetry");

        logger.ZLogInformation($"startup complete");
        telemetry.Dispose();

        string output = ReadOutput(stream);
        string line = Assert.Single(SplitLines(output));

        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[INF\] Royale\.Tests\.Telemetry: startup complete$", line);
    }

    [Fact]
    public void OtlpEndpointEnablesExporterSetupWithoutReachableCollector()
    {
        using var stream = new NonClosingMemoryStream();
        RoyaleTelemetry telemetry = RoyaleTelemetry.CreateServerForTesting(
            stream,
            LogLevel.Information,
            new RoyaleTelemetryEnvironment("http://localhost:4317", null, null));

        Assert.True(telemetry.OtlpExportEnabled);
        Assert.True(telemetry.HasTracerProvider);
        Assert.True(telemetry.HasMeterProvider);

        telemetry.Dispose();
    }

    [Fact]
    public void SdkDisabledSuppressesExporterSetup()
    {
        using var stream = new NonClosingMemoryStream();
        RoyaleTelemetry telemetry = RoyaleTelemetry.CreateServerForTesting(
            stream,
            LogLevel.Information,
            new RoyaleTelemetryEnvironment("http://localhost:4317", "true", null));

        Assert.False(telemetry.OtlpExportEnabled);
        Assert.False(telemetry.HasTracerProvider);
        Assert.False(telemetry.HasMeterProvider);

        telemetry.Dispose();
    }

    private static string ReadOutput(MemoryStream stream)
    {
        stream.Position = 0;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string[] SplitLines(string output)
    {
        return output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class NonClosingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            Flush();
        }
    }
}
