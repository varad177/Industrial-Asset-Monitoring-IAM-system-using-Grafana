namespace IAM.Application.Configuration;

/// <summary>
/// Strongly typed configuration for InfluxDB connection.
/// Bound to the "InfluxDbSettings" section in appsettings.json.
/// </summary>
public sealed class InfluxDbSettings
{
    public const string SectionName = "InfluxDbSettings";

    public required string Url { get; init; }
    public required string Token { get; init; }
    public required string Organization { get; init; }
    public required string Bucket { get; init; }
}
