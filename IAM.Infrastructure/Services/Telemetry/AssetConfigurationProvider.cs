using System.Text.Json;
using IAM.Application.Interfaces;
using IAM.Domain.Entities.Telemetry;
using Microsoft.Extensions.Logging;

namespace IAM.Infrastructure.Services.Telemetry;

/// <summary>
/// Loads and caches asset configuration from the assets.json file.
/// Registered as a singleton since the configuration is static at runtime.
/// </summary>
public sealed class AssetConfigurationProvider : IAssetConfigurationProvider
{
    private readonly AssetConfiguration _configuration;
    private readonly Dictionary<string, Asset> _assetLookup;
    private readonly ILogger<AssetConfigurationProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AssetConfigurationProvider(ILogger<AssetConfigurationProvider> logger)
    {
        _logger = logger;

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "assets.json");

        if (!File.Exists(jsonPath))
        {
            _logger.LogError("Asset configuration file not found at: {Path}", jsonPath);
            throw new FileNotFoundException("assets.json not found.", jsonPath);
        }

        var json = File.ReadAllText(jsonPath);
        _configuration = JsonSerializer.Deserialize<AssetConfiguration>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize assets.json");

        _assetLookup = _configuration.Assets.ToDictionary(a => a.AssetId, StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Loaded {AssetCount} assets with {SignalCount} total signals from assets.json",
            _configuration.Assets.Count,
            _configuration.Assets.Sum(a => a.Signals.Count));
    }

    public AssetConfiguration GetConfiguration() => _configuration;

    public Asset? GetAsset(string assetId) =>
        _assetLookup.TryGetValue(assetId, out var asset) ? asset : null;

    public IReadOnlyList<Asset> GetAllAssets() => _configuration.Assets;
}
