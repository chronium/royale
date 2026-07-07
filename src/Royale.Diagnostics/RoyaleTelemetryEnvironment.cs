namespace Royale.Diagnostics;

internal readonly record struct RoyaleTelemetryEnvironment(
    string? OtlpEndpoint,
    string? SdkDisabled,
    string? ServiceName,
    string? OtlpTimeout = null,
    string? OtlpLogsTimeout = null,
    string? OtlpTracesTimeout = null,
    string? OtlpMetricsTimeout = null,
    string? BatchSpanProcessorExportTimeout = null,
    string? BatchLogRecordProcessorExportTimeout = null,
    string? MetricExportTimeout = null)
{
    public bool OtlpExportEnabled =>
        !IsSdkDisabled && !string.IsNullOrWhiteSpace(OtlpEndpoint);

    public bool IsSdkDisabled =>
        string.Equals(SdkDisabled?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    public string EffectiveServiceName =>
        string.IsNullOrWhiteSpace(ServiceName)
            ? RoyaleTelemetry.DefaultServerServiceName
            : ServiceName.Trim();

    public bool HasConfiguredOtlpTimeout =>
        !string.IsNullOrWhiteSpace(OtlpTimeout) ||
        !string.IsNullOrWhiteSpace(OtlpLogsTimeout) ||
        !string.IsNullOrWhiteSpace(OtlpTracesTimeout) ||
        !string.IsNullOrWhiteSpace(OtlpMetricsTimeout);

    public bool HasConfiguredTraceProcessorTimeout =>
        !string.IsNullOrWhiteSpace(BatchSpanProcessorExportTimeout);

    public bool HasConfiguredLogProcessorTimeout =>
        !string.IsNullOrWhiteSpace(BatchLogRecordProcessorExportTimeout);

    public bool HasConfiguredMetricReaderTimeout =>
        !string.IsNullOrWhiteSpace(MetricExportTimeout);

    public static RoyaleTelemetryEnvironment FromProcess() => new(
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"),
        Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED"),
        Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME"),
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TIMEOUT"),
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_TIMEOUT"),
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_TIMEOUT"),
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_TIMEOUT"),
        Environment.GetEnvironmentVariable("OTEL_BSP_EXPORT_TIMEOUT"),
        Environment.GetEnvironmentVariable("OTEL_BLRP_EXPORT_TIMEOUT"),
        Environment.GetEnvironmentVariable("OTEL_METRIC_EXPORT_TIMEOUT"));
}
