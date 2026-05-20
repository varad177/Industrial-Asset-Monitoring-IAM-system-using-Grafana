using IAM.Application.Interfaces;
using IAM.Domain.Entities.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IAM.Infrastructure.Services.Telemetry;

/// <summary>
/// Background service that continuously generates realistic sensor telemetry data
/// and writes it to InfluxDB every second.
/// Uses smooth random walk for natural-looking signal fluctuation.
/// </summary>
public sealed class TelemetrySeederBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAssetConfigurationProvider _assetProvider;
    private readonly ILogger<TelemetrySeederBackgroundService> _logger;
    private readonly Random _random = new();

    /// <summary>Tracks the "current" value for each signal so values drift smoothly.</summary>
    private readonly Dictionary<string, double> _currentValues = new();

    private const int SeedIntervalMs = 1000; // 1 second

    public TelemetrySeederBackgroundService(
        IServiceScopeFactory scopeFactory,
        IAssetConfigurationProvider assetProvider,
        ILogger<TelemetrySeederBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _assetProvider = assetProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 Telemetry Seeder started — generating data every {Interval}ms", SeedIntervalMs);

        // Initialize current values at the midpoint of each signal's range
        foreach (var asset in _assetProvider.GetAllAssets())
        {
            foreach (var signal in asset.Signals)
            {
                var key = $"{asset.AssetId}:{signal.SignalId}";
                _currentValues[key] = (signal.MinValue + signal.MaxValue) / 2.0;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var records = GenerateTelemetryBatch();

                await using var scope = _scopeFactory.CreateAsyncScope();
                var influxService = scope.ServiceProvider.GetRequiredService<IInfluxDbService>();
                await influxService.WriteTelemetryAsync(records, stoppingToken);

                _logger.LogDebug("📡 Seeded {Count} telemetry points", records.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error seeding telemetry data");
            }

            await Task.Delay(SeedIntervalMs, stoppingToken);
        }

        _logger.LogInformation("🛑 Telemetry Seeder stopped");
    }

    /// <summary>
    /// Generates one data point per signal using a random-walk approach
    /// that keeps values within the configured min/max bounds.
    /// </summary>
    private List<TelemetryRecord> GenerateTelemetryBatch()
    {
        var now = DateTime.UtcNow;
        var records = new List<TelemetryRecord>();

        foreach (var asset in _assetProvider.GetAllAssets())
        {
            foreach (var signal in asset.Signals)
            {
                var key = $"{asset.AssetId}:{signal.SignalId}";
                var range = signal.MaxValue - signal.MinValue;

                // Random walk: drift ±3% of range
                var drift = (_random.NextDouble() - 0.5) * 2.0 * range * 0.03;
                var currentValue = _currentValues[key] + drift;

                // Clamp within bounds
                currentValue = Math.Clamp(currentValue, signal.MinValue, signal.MaxValue);

                // Add slight noise for realism (±0.5% of range)
                var noise = (_random.NextDouble() - 0.5) * range * 0.005;
                var finalValue = Math.Clamp(currentValue + noise, signal.MinValue, signal.MaxValue);

                _currentValues[key] = currentValue; // store without noise for next iteration

                records.Add(new TelemetryRecord
                {
                    Timestamp = now,
                    AssetId = asset.AssetId,
                    SignalId = signal.SignalId,
                    Value = Math.Round(finalValue, 2)
                });
            }
        }

        return records;
    }
}
