namespace IAM.Domain.Entities.Telemetry;

/// <summary>
/// Root configuration object that holds the full list of industrial assets.
/// Deserialized directly from assets.json.
/// </summary>
public sealed class AssetConfiguration
{
    public required IReadOnlyList<Asset> Assets { get; init; }
}
