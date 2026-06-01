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

    // ─── Build signal filter (single or multi CSV) ───────────────────────────
    var signalFilter = string.Empty;

    if (!string.IsNullOrWhiteSpace(signalId))
    {
        var signals = signalId
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (signals.Length == 1)
        {
            // Single signal — simple equality
            signalFilter = $"|> filter(fn: (r) => r[\"signalId\"] == \"{signals[0]}\")";
        }
        else
        {
            // Multiple signals — OR condition
            var conditions = string.Join(" or ",
                signals.Select(s => $"r[\"signalId\"] == \"{s}\""));
            signalFilter = $"|> filter(fn: (r) => {conditions})";
        }
    }
    // ─────────────────────────────────────────────────────────────────────────

    // ─── Clamp sub-minute intervals to 1m for InfluxDB stability ─────────────
    var safeInterval = interval.EndsWith("s") ? "1m" : interval;
    // ─────────────────────────────────────────────────────────────────────────

    var flux = $"""
        from(bucket: "{_settings.Bucket}")
          |> range(start: {rangeStart}{rangeStop})
          |> filter(fn: (r) => r["_measurement"] == "{MeasurementName}")
          |> filter(fn: (r) => r["assetId"] == "{assetId}")
          {signalFilter}
          |> filter(fn: (r) => r["_field"] == "value")
          |> aggregateWindow(every: {safeInterval}, fn: {fluxAggFn}, createEmpty: false)
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
                Interval: safeInterval));
        }
    }

    _logger.LogInformation(
        "Aggregated query returned {Count} records — {Agg}/{Interval} for asset {AssetId}, Signals: {Signals}",
        results.Count, aggregation, safeInterval, assetId, signalId ?? "all");

    return results;
}
    
    
    public void Dispose()
    {
        _client.Dispose();
        _logger.LogInformation("InfluxDB client disposed");
    }
}
