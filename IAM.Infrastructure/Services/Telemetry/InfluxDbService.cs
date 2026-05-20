using IAM.Application.Configuration;
using IAM.Application.DTOs.Telemetry;
using IAM.Application.Interfaces;
using IAM.Domain.Entities.Telemetry;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IAM.Infrastructure.Services.Telemetry;

/// <summary>
/// Production-quality InfluxDB service for writing and querying telemetry data.
/// Uses the InfluxDB.Client SDK with Flux queries.
/// </summary>
public sealed class InfluxDbService : IInfluxDbService, IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly InfluxDbSettings _settings;
    private readonly ILogger<InfluxDbService> _logger;

    private const string MeasurementName = "telemetry";

    public InfluxDbService(IOptions<InfluxDbSettings> settings, ILogger<InfluxDbService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _client = new InfluxDBClient(_settings.Url, _settings.Token);

        _logger.LogInformation(
            "InfluxDB client initialized — Url: {Url}, Org: {Org}, Bucket: {Bucket}",
            _settings.Url, _settings.Organization, _settings.Bucket);
    }

    /// <inheritdoc />
    public async Task WriteTelemetryAsync(IEnumerable<TelemetryRecord> records, CancellationToken ct = default)
    {
        var writeApi = _client.GetWriteApiAsync();

        var points = records.Select(r =>
            PointData
                .Measurement(MeasurementName)
                .Tag("assetId", r.AssetId)
                .Tag("signalId", r.SignalId)
                .Field("value", r.Value)
                .Timestamp(r.Timestamp, WritePrecision.Ms))
            .ToList();

        if (points.Count == 0) return;

        await writeApi.WritePointsAsync(points, _settings.Bucket, _settings.Organization, ct);

        _logger.LogDebug("Wrote {Count} telemetry points to InfluxDB", points.Count);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TelemetryDataDto>> QueryTelemetryAsync(
        string assetId,
        DateTime from,
        DateTime to,
        string? signalId = null,
        CancellationToken ct = default)
    {
        var fromUtc = from.ToUniversalTime().ToString("o");
        var toUtc = to.ToUniversalTime().ToString("o");

        var signalFilter = string.IsNullOrWhiteSpace(signalId)
            ? string.Empty
            : $"|> filter(fn: (r) => r[\"signalId\"] == \"{signalId}\")";

        var flux = $"""
            from(bucket: "{_settings.Bucket}")
              |> range(start: {fromUtc}, stop: {toUtc})
              |> filter(fn: (r) => r["_measurement"] == "{MeasurementName}")
              |> filter(fn: (r) => r["assetId"] == "{assetId}")
              {signalFilter}
              |> filter(fn: (r) => r["_field"] == "value")
              |> sort(columns: ["_time"])
            """;

        _logger.LogDebug("Executing Flux query for asset {AssetId}: {Query}", assetId, flux);

        var queryApi = _client.GetQueryApi();
        var tables = await queryApi.QueryAsync(flux, _settings.Organization, ct);

        var results = new List<TelemetryDataDto>();

        foreach (var table in tables)
        {
            foreach (var record in table.Records)
            {
                results.Add(new TelemetryDataDto(
                    Timestamp: record.GetTimeInDateTime()!.Value,
                    AssetId: record.GetValueByKey("assetId")?.ToString() ?? assetId,
                    SignalId: record.GetValueByKey("signalId")?.ToString() ?? string.Empty,
                    Value: Convert.ToDouble(record.GetValue())));
            }
        }

        _logger.LogInformation(
            "Query returned {Count} records for asset {AssetId} [{From} → {To}]",
            results.Count, assetId, fromUtc, toUtc);

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AggregatedTelemetryDto>> QueryAggregatedTelemetryAsync(
        string assetId,
        string aggregation,
        string interval,
        DateTime? from = null,
        DateTime? to = null,
        string? signalId = null,
        CancellationToken ct = default)
    {
        var validAggregations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "avg", "min", "max", "sum" };
        if (!validAggregations.Contains(aggregation))
            throw new ArgumentException($"Unsupported aggregation: {aggregation}. Supported: avg, min, max, sum.");

        // Map user-friendly names to Flux aggregation functions
        var fluxAggFn = aggregation.ToLowerInvariant() switch
        {
            "avg" => "mean",
            "min" => "min",
            "max" => "max",
            "sum" => "sum",
            _ => "mean"
        };

        var rangeStart = from?.ToUniversalTime().ToString("o") ?? "-1h";
        var rangeStop = to != null ? $", stop: {to.Value.ToUniversalTime():o}" : string.Empty;

        var signalFilter = string.IsNullOrWhiteSpace(signalId)
            ? string.Empty
            : $"|> filter(fn: (r) => r[\"signalId\"] == \"{signalId}\")";

        var flux = $"""
            from(bucket: "{_settings.Bucket}")
              |> range(start: {rangeStart}{rangeStop})
              |> filter(fn: (r) => r["_measurement"] == "{MeasurementName}")
              |> filter(fn: (r) => r["assetId"] == "{assetId}")
              {signalFilter}
              |> filter(fn: (r) => r["_field"] == "value")
              |> aggregateWindow(every: {interval}, fn: {fluxAggFn}, createEmpty: false)
              |> yield(name: "{fluxAggFn}")
            """;

        _logger.LogDebug("Executing aggregated Flux query: {Query}", flux);

        var queryApi = _client.GetQueryApi();
        var tables = await queryApi.QueryAsync(flux, _settings.Organization, ct);

        var results = new List<AggregatedTelemetryDto>();

        foreach (var table in tables)
        {
            foreach (var record in table.Records)
            {
                results.Add(new AggregatedTelemetryDto(
                    Timestamp: record.GetTimeInDateTime()!.Value,
                    AssetId: record.GetValueByKey("assetId")?.ToString() ?? assetId,
                    SignalId: record.GetValueByKey("signalId")?.ToString() ?? string.Empty,
                    Value: Convert.ToDouble(record.GetValue()),
                    Aggregation: aggregation.ToLowerInvariant(),
                    Interval: interval));
            }
        }

        _logger.LogInformation(
            "Aggregated query returned {Count} records — {Agg}/{Interval} for asset {AssetId}",
            results.Count, aggregation, interval, assetId);

        return results;
    }

    public void Dispose()
    {
        _client.Dispose();
        _logger.LogInformation("InfluxDB client disposed");
    }
}
