namespace IAM.Application.DTOs.Telemetry;

/// <summary>DTO for an asset summary returned by the API.</summary>
public sealed record AssetDto(string AssetId, string AssetName, int SignalCount);

/// <summary>DTO for a signal definition within an asset.</summary>
public sealed record SignalDto(
    string SignalId,
    string SignalName,
    string Unit,
    double MinValue,
    double MaxValue);

/// <summary>DTO for a single telemetry data point.</summary>
public sealed record TelemetryDataDto(
    DateTime Timestamp,
    string AssetId,
    string SignalId,
    double Value);

/// <summary>DTO for an aggregated telemetry result.</summary>
public sealed record AggregatedTelemetryDto(
    DateTime Timestamp,
    string AssetId,
    string SignalId,
    double Value,
    string Aggregation,
    string Interval);
