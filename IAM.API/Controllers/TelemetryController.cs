using System.Security.Claims;
using IAM.Application.DTOs.Telemetry;
using IAM.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IAM.API.Controllers;

/// <summary>
/// Telemetry API for querying industrial asset data from InfluxDB.
/// Provides endpoints for assets, signals, raw telemetry, and aggregated queries.
/// All endpoints require authentication (JWT or Grafana API Key).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TelemetryController : ControllerBase
{
    private readonly IInfluxDbService _influxDbService;
    private readonly IAssetConfigurationProvider _assetProvider;
    private readonly IOpenFgaService _fgaService;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        IInfluxDbService influxDbService,
        IAssetConfigurationProvider assetProvider,
        IOpenFgaService fgaService,
        ILogger<TelemetryController> logger)
    {
        _influxDbService = influxDbService;
        _assetProvider = assetProvider;
        _fgaService = fgaService;
        _logger = logger;
    }

    /// <summary>
    /// Get the authenticated user's email from ClaimsPrincipal.
    /// Works seamlessly for both JWT (cookie/header) and Grafana API Key auth.
    /// </summary>
    private string? GetAuthenticatedUser()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;
    }

    [HttpGet("assets")]
    public IActionResult GetAssets()
    {
        _logger.LogInformation("Fetching all assets for user {User}", GetAuthenticatedUser());

        var assets = _assetProvider.GetAllAssets()
            .Select(a => new AssetDto(a.AssetId, a.AssetName, a.Signals.Count))
            .ToList();

        return Ok(assets);
    }


    [HttpGet("assets/{assetId}/signals")]
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

    [HttpGet("data")]
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

        var userId = GetAuthenticatedUser()!;
        _logger.LogInformation(
            "Querying telemetry — User: {User}, Asset: {AssetId}, From: {From}, To: {To}, Signal: {SignalId}",
            userId, assetId, from, to, signalId ?? "all");

        // OpenFGA check
        var hasAccess = await _fgaService.CheckAsync(userId, "viewer", "asset", assetId);
        if (!hasAccess)
            return StatusCode(403, new { message = $"User '{userId}' lacks viewer access to asset '{assetId}'." });

        var data = await _influxDbService.QueryTelemetryAsync(assetId, from, to, signalId, ct);
        return Ok(data);
    }


    [HttpGet("aggregate")]
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

        var userId = GetAuthenticatedUser()!;
        _logger.LogInformation(
            "Querying aggregated telemetry — User: {User}, Asset: {AssetId}, Agg: {Agg}, Interval: {Interval}",
            userId, assetId, aggregation, interval);

        // OpenFGA check
        var hasAccess = await _fgaService.CheckAsync(userId, "viewer", "asset", assetId);
        if (!hasAccess)
            return StatusCode(403, new { message = $"User '{userId}' lacks viewer access to asset '{assetId}'." });

        var data = await _influxDbService.QueryAggregatedTelemetryAsync(
            assetId, aggregation, interval, from, to, signalId, ct);

        return Ok(data);
    }
}
