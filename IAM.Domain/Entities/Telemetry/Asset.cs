namespace IAM.Domain.Entities.Telemetry;

/// <summary>
/// Represents an industrial asset (e.g. Boiler, Turbine) with its associated signals.
/// </summary>
public sealed class Asset
{
    public required string AssetId { get; init; }
    public required string AssetName { get; init; }
    public required IReadOnlyList<Signal> Signals { get; init; }
}
