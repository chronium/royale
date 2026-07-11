using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Royale.Diagnostics.Logging;
using ZLogger;

namespace Royale.Diagnostics.Telemetry;

public sealed class RoyaleTelemetry : IDisposable
{
    public const string DefaultServerServiceName = "royale-server";
    public const string ServerActivitySourceName = "Royale.Server";
    public const string ServerMeterName = "Royale.Server";

    private const int DefaultOtlpExportTimeoutMilliseconds = 1_000;

    public static ActivitySource ServerActivitySource { get; } = new(ServerActivitySourceName);

    public static Meter ServerMeter { get; } = new(ServerMeterName);

    private readonly TracerProvider? tracerProvider;
    private readonly MeterProvider? meterProvider;
    private bool disposed;

    private RoyaleTelemetry(
        ILoggerFactory loggerFactory,
        TracerProvider? tracerProvider,
        MeterProvider? meterProvider,
        bool otlpExportEnabled)
    {
        LoggerFactory = loggerFactory;
        this.tracerProvider = tracerProvider;
        this.meterProvider = meterProvider;
        OtlpExportEnabled = otlpExportEnabled;
    }

    public ILoggerFactory LoggerFactory { get; }

    public bool OtlpExportEnabled { get; }

    internal bool HasTracerProvider => tracerProvider is not null;

    internal bool HasMeterProvider => meterProvider is not null;

    public static RoyaleTelemetry CreateServer(LogLevel minimumLevel)
    {
        return CreateServer(
            minimumLevel,
            RoyaleTelemetryEnvironment.FromProcess(),
            logging => logging.AddZLoggerConsole(RoyaleLogging.ConfigureZLoggerOptions));
    }

    internal static RoyaleTelemetry CreateServerForTesting(
        Stream logStream,
        LogLevel minimumLevel,
        RoyaleTelemetryEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(logStream);

        return CreateServer(
            minimumLevel,
            environment,
            logging => logging.AddZLoggerStream(logStream, RoyaleLogging.ConfigureZLoggerOptions));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        LoggerFactory.Dispose();
        tracerProvider?.Dispose();
        meterProvider?.Dispose();
        disposed = true;
    }

    private static RoyaleTelemetry CreateServer(
        LogLevel minimumLevel,
        RoyaleTelemetryEnvironment environment,
        Action<ILoggingBuilder> configureConsoleLogging)
    {
        bool otlpExportEnabled = environment.OtlpExportEnabled;
        if (otlpExportEnabled)
        {
            _ = CreateOtlpEndpoint(environment.OtlpEndpoint);
        }

        ILoggerFactory loggerFactory = CreateLoggerFactory(
            minimumLevel,
            environment.EffectiveServiceName,
            otlpExportEnabled,
            environment,
            configureConsoleLogging);
        TracerProvider? tracerProvider = null;
        MeterProvider? meterProvider = null;

        try
        {
            if (otlpExportEnabled)
            {
                tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(CreateResourceBuilder(environment.EffectiveServiceName))
                    .AddSource(ServerActivitySourceName)
                    .AddOtlpExporter(options => ConfigureTraceExporter(options, environment))
                    .Build();

                meterProvider = Sdk.CreateMeterProviderBuilder()
                    .SetResourceBuilder(CreateResourceBuilder(environment.EffectiveServiceName))
                    .AddMeter(ServerMeterName)
                    .AddOtlpExporter((exporter, reader) =>
                    {
                        ConfigureOtlpExporter(exporter, environment);
                        ConfigureMetricReader(reader, environment);
                    })
                    .Build();
            }

            return new RoyaleTelemetry(loggerFactory, tracerProvider, meterProvider, otlpExportEnabled);
        }
        catch
        {
            loggerFactory.Dispose();
            tracerProvider?.Dispose();
            meterProvider?.Dispose();
            throw;
        }
    }

    private static ILoggerFactory CreateLoggerFactory(
        LogLevel minimumLevel,
        string serviceName,
        bool otlpExportEnabled,
        RoyaleTelemetryEnvironment environment,
        Action<ILoggingBuilder> configureConsoleLogging)
    {
        return Microsoft.Extensions.Logging.LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(minimumLevel);
            configureConsoleLogging(logging);

            if (otlpExportEnabled)
            {
                logging.AddOpenTelemetry(options =>
                {
                    options.SetResourceBuilder(CreateResourceBuilder(serviceName));
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                    options.ParseStateValues = true;
                    options.AddOtlpExporter((exporter, processor) =>
                    {
                        ConfigureOtlpExporter(exporter, environment);
                        ConfigureLogProcessor(processor, environment);
                    });
                });
            }
        });
    }

    private static ResourceBuilder CreateResourceBuilder(string serviceName)
    {
        return ResourceBuilder.CreateDefault().AddService(serviceName);
    }

    private static Uri CreateOtlpEndpoint(string? endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        throw new InvalidOperationException(
            "OTEL_EXPORTER_OTLP_ENDPOINT must be an absolute URI when OpenTelemetry export is enabled.");
    }

    private static void ConfigureOtlpExporter(
        OtlpExporterOptions options,
        RoyaleTelemetryEnvironment environment)
    {
        if (!environment.HasConfiguredOtlpTimeout)
        {
            options.TimeoutMilliseconds = DefaultOtlpExportTimeoutMilliseconds;
        }
    }

    private static void ConfigureTraceExporter(
        OtlpExporterOptions options,
        RoyaleTelemetryEnvironment environment)
    {
        ConfigureOtlpExporter(options, environment);

        if (!environment.HasConfiguredTraceProcessorTimeout)
        {
            options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds =
                DefaultOtlpExportTimeoutMilliseconds;
        }
    }

    private static void ConfigureLogProcessor(
        LogRecordExportProcessorOptions options,
        RoyaleTelemetryEnvironment environment)
    {
        if (!environment.HasConfiguredLogProcessorTimeout)
        {
            options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds =
                DefaultOtlpExportTimeoutMilliseconds;
        }
    }

    private static void ConfigureMetricReader(
        MetricReaderOptions options,
        RoyaleTelemetryEnvironment environment)
    {
        if (!environment.HasConfiguredMetricReaderTimeout)
        {
            options.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds =
                DefaultOtlpExportTimeoutMilliseconds;
        }
    }
}
