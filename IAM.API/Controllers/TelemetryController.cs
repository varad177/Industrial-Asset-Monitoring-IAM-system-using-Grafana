using IAM.Application.DTOs.Telemetry;
using IAM.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IAM.API.Controllers;

/// <summary>
/// Telemetry API for querying industrial asset data from InfluxDB.
/// Provides endpoints for assets, signals, raw telemetry, and aggregated queries.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TelemetryController : ControllerBase
{
    private readonly IInfluxDbService _influxDbService;
    private readonly IAssetConfigurationProvider _assetProvider;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        IInfluxDbService influxDbService,
        IAssetConfigurationProvider assetProvider,
        ILogger<TelemetryController> logger)
    {
        _influxDbService = influxDbService;
        _assetProvider = assetProvider;
        _logger = logger;
    }

    /// <summary>Get all configured industrial assets.</summary>
    /// <response code="200">List of all assets with signal counts.</response>
    [HttpGet("assets")]
    [ProducesResponseType(typeof(IEnumerable<AssetDto>), StatusCodes.Status200OK)]
    public IActionResult GetAssets()
    {
        _logger.LogInformation("Fetching all assets");

        var assets = _assetProvider.GetAllAssets()
            .Select(a => new AssetDto(a.AssetId, a.AssetName, a.Signals.Count))
            .ToList();

        return Ok(assets);
    }

    /// <summary>Get all signals for a specific asset.</summary>
    /// <param name="assetId">The asset identifier (e.g. asset_1).</param>
    /// <response code="200">List of signals for the asset.</response>
    /// <response code="404">Asset not found.</response>
    [HttpGet("assets/{assetId}/signals")]
    [ProducesResponseType(typeof(IEnumerable<SignalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSignals(string assetId)
    {
        _logger.LogInformation("Fetching signals for asset {AssetId}", assetId);

        var asset = _assetProvider.GetAsset(assetId);
        if (asset is null)
            return NotFound(new { message = $"Asset '{assetId}' not found." });

        var signals = asset.Signals
            .Select(s => new SignalDto(s.SignalId, s.SignalName, s.Unit, s.MinValue, s.MaxValue))
            .ToList();

        return Ok(signals);
    }

    /// <summary>Get raw telemetry data for an asset within a time range.</summary>
    /// <param name="assetId">The asset identifier.</param>
    /// <param name="from">Start of the time range (ISO 8601).</param>
    /// <param name="to">End of the time range (ISO 8601).</param>
    /// <param name="signalId">Optional: filter by a specific signal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Raw telemetry data points.</response>
    /// <response code="400">Invalid parameters.</response>
    [HttpGet("data")]
    [ProducesResponseType(typeof(IEnumerable<TelemetryDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTelemetryData(
        [FromQuery] string assetId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? signalId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assetId))
            return BadRequest(new { message = "assetId is required." });

        if (from >= to)
            return BadRequest(new { message = "'from' must be before 'to'." });

        _logger.LogInformation(
            "Querying telemetry — Asset: {AssetId}, From: {From}, To: {To}, Signal: {SignalId}",
            assetId, from, to, signalId ?? "all");

        var data = await _influxDbService.QueryTelemetryAsync(assetId, from, to, signalId, ct);
        return Ok(data);
    }

    /// <summary>Get aggregated telemetry data for an asset.</summary>
    /// <param name="assetId">The asset identifier.</param>
    /// <param name="aggregation">Aggregation function: avg, min, max, sum.</param>
    /// <param name="interval">Time window interval (e.g. 1m, 5m, 1h).</param>
    /// <param name="from">Optional start of the time range.</param>
    /// <param name="to">Optional end of the time range.</param>
    /// <param name="signalId">Optional: filter by a specific signal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Aggregated telemetry data points.</response>
    /// <response code="400">Invalid parameters.</response>
    [HttpGet("aggregate")]
    [ProducesResponseType(typeof(IEnumerable<AggregatedTelemetryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAggregatedTelemetry(
        [FromQuery] string assetId,
        [FromQuery] string aggregation = "avg",
        [FromQuery] string interval = "1m",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? signalId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assetId))
            return BadRequest(new { message = "assetId is required." });

        var validAggregations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "avg", "min", "max", "sum" };
        if (!validAggregations.Contains(aggregation))
            return BadRequest(new { message = $"Unsupported aggregation '{aggregation}'. Supported: avg, min, max, sum." });

        _logger.LogInformation(
            "Querying aggregated telemetry — Asset: {AssetId}, Agg: {Agg}, Interval: {Interval}",
            assetId, aggregation, interval);

        var data = await _influxDbService.QueryAggregatedTelemetryAsync(
            assetId, aggregation, interval, from, to, signalId, ct);

        return Ok(data);
    }
}
