namespace IAM.Domain.Entities.Telemetry;

/// <summary>
/// Represents a single sensor signal within an industrial asset.
/// </summary>
public sealed class Signal
{
    public required string SignalId { get; init; }
    public required string SignalName { get; init; }
    public required string Unit { get; init; }
    public required double MinValue { get; init; }
    public required double MaxValue { get; init; }
}
