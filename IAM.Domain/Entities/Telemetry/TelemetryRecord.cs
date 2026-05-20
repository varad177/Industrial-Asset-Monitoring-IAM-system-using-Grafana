namespace IAM.Domain.Entities.Telemetry;

/// <summary>
/// Represents a single telemetry data point written to / read from InfluxDB.
/// </summary>
public sealed class TelemetryRecord
{
    public required DateTime Timestamp { get; init; }
    public required string AssetId { get; init; }
    public required string SignalId { get; init; }
    public required double Value { get; init; }
}
