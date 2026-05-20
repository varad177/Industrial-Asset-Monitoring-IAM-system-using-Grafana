using IAM.Domain.Entities.Telemetry;

namespace IAM.Application.Interfaces;

/// <summary>
/// Provides access to the industrial asset configuration loaded from assets.json.
/// </summary>
public interface IAssetConfigurationProvider
{
    /// <summary>Get the full asset configuration.</summary>
    AssetConfiguration GetConfiguration();

    /// <summary>Get a specific asset by ID.</summary>
    Asset? GetAsset(string assetId);

    /// <summary>Get all assets.</summary>
    IReadOnlyList<Asset> GetAllAssets();
}
