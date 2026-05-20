namespace IAM.Application.Configuration;

/// <summary>
/// Strongly typed configuration for OpenFGA.
/// Bound to "OpenFgaSettings" in appsettings.json.
/// </summary>
public sealed class OpenFgaSettings
{
    public const string SectionName = "OpenFgaSettings";

    public required string ApiUrl { get; init; }
    public required string StoreId { get; init; }
}
