using IAM.Application.DTOs.Telemetry;
using IAM.Domain.Entities.Telemetry;

namespace IAM.Application.Interfaces;

/// <summary>
/// Abstraction over InfluxDB operations for telemetry data.
/// </summary>
public interface IInfluxDbService
{
    /// <summary>Write a batch of telemetry records to InfluxDB.</summary>
    Task WriteTelemetryAsync(IEnumerable<TelemetryRecord> records, CancellationToken ct = default);

    /// <summary>Query raw telemetry data for an asset within a time range.</summary>
    Task<IReadOnlyList<TelemetryDataDto>> QueryTelemetryAsync(
        string assetId,
        DateTime from,
        DateTime to,
        string? signalId = null,
        CancellationToken ct = default);

    /// <summary>Query aggregated telemetry data.</summary>
    Task<IReadOnlyList<AggregatedTelemetryDto>> QueryAggregatedTelemetryAsync(
        string assetId,
        string aggregation,
        string interval,
        DateTime? from = null,
        DateTime? to = null,
        string? signalId = null,
        CancellationToken ct = default);
}
