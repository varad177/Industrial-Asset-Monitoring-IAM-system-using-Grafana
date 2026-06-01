using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IAM.Application.DTOs.Authorization;
using IAM.Application.Interfaces;
using IAM.Infrastructure.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IAM.API.Controllers;

/// <summary>
/// Authorization-aware APIs for assets and Grafana dashboards.
/// Uses OpenFGA to filter results to only what the authenticated user can access.
/// JWT authentication is required — the user identity is extracted from the token.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
[Produces("application/json")]
public class AssetAuthController : ControllerBase
{
    private readonly IOpenFgaService _fga;
    private readonly IAssetConfigurationProvider _assetProvider;
    private readonly ILogger<AssetAuthController> _logger;

  
    private const string TelemetryDashboardUid = "iam-asset-telemetry";
    

    public AssetAuthController(
        IOpenFgaService fga,
        IAssetConfigurationProvider assetProvider,
        ILogger<AssetAuthController> logger)
    {
        _fga = fga;
        _assetProvider = assetProvider;
        _logger = logger;
    }


    /// <summary>
    /// Get all assets the current user is authorized to view.
    /// Checks OpenFGA for viewer relation on each asset.
    /// </summary>
    /// 
    [HttpGet("assets")]
    public async Task<IActionResult> GetAuthorizedAssets(CancellationToken ct)
    {
        var userId = GetUserId();
        _logger.LogInformation("Fetching authorized assets for user: {UserId}", userId);

        // Ask OpenFGA which assets this user can view
        var allowedIds = await _fga.GetAllowedObjectsAsync(userId, "viewer", "asset", ct);

        // Filter the in-memory asset config to only allowed ones
        var allAssets = _assetProvider.GetAllAssets();
        var result = allAssets
            .Where(a => allowedIds.Contains(a.AssetId, StringComparer.OrdinalIgnoreCase))
            .Select(a => new AuthorizedAssetDto(a.AssetId, a.AssetName, a.Signals.Count))
            .ToList();

        _logger.LogInformation("User {UserId} has access to {Count}/{Total} assets",
            userId, result.Count, allAssets.Count);

        return Ok(result);
    }

    /// <summary>
    /// Get Grafana dashboard URL for ONE specific asset.
    /// The user selects the asset in React; this endpoint verifies access via OpenFGA
    /// and returns a secure proxy URL with that single asset locked in.
    /// Grafana then shows the $signalId variable for signal-level filtering.
    /// 
    /// The URL is a proxy endpoint (/api/grafana-proxy/*) that:
    /// - Extracts JWT from HTTP-only cookie
    /// - Forwards to Grafana with X-JWT-Assertion header
    /// - Never exposes token in URL
    /// </summary>
    
    [HttpGet("dashboards/asset_telemetry/url")]
public async Task<IActionResult> GetDashboardUrl(
    [FromQuery] string assetId,
    [FromQuery] string from = "now-30m",
    [FromQuery] string to = "now",
    CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(assetId))
        return BadRequest(new { message = "assetId is required." });

    var userId = GetUserId();

    // Check dashboard access
    var canViewDashboard = await _fga.CheckAsync(
        userId,
        "viewer",
        "dashboard",
        TelemetryDashboardUid,
        ct);

    if (!canViewDashboard)
        return Forbid();

    // Check asset access
    var canViewAsset = await _fga.CheckAsync(
        userId,
        "viewer",
        "asset",
        assetId,
        ct);

    if (!canViewAsset)
        return Forbid();

    // Validate asset exists
    var asset = _assetProvider.GetAsset(assetId);

    if (asset is null)
        return NotFound(new
        {
            message = $"Asset '{assetId}' not found."
        });

    // Return DIRECT Grafana URL
    // The API proxy will inject JWT header automatically
    var grafanaUrl =
        $"http://localhost:5500/grafana/d/{TelemetryDashboardUid}/{TelemetryDashboardUid}" +
        $"?orgId=1" +
        $"&from={Uri.EscapeDataString(from)}" +
        $"&to={Uri.EscapeDataString(to)}" +
        $"&var-assetId={Uri.EscapeDataString(assetId)}" +
        $"&refresh=5s";

    return Ok(new DashboardUrlDto(
        grafanaUrl,
        TelemetryDashboardUid,
        [assetId]
    ));
}

    
    
    
    
    
    
    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Extract the user identifier from the JWT claims.
    /// Uses email claim first, falls back to NameIdentifier (user ID).
    /// OpenFGA tuples use the email/username as the user key.
    /// </summary>
    private string GetUserId()
    {
        // Try email first (matches OpenFGA tuple user keys like "user:admin@company.com")
        var email = User.FindFirstValue(ClaimTypes.Email);
        // here user is coming from jwt token and in jwt token we are using email as nameidentifier claim so we can directly use that as well instead of email claim
        if (!string.IsNullOrEmpty(email))
            return email;

        // Fallback to NameIdentifier
        return User.FindFirstValue(ClaimTypes.NameIdentifier) // name identifier is typically the user ID, but in our case we use email as name identifier claim in jwt token so this will also return email
            ?? User.FindFirstValue(ClaimTypes.Name) // else name 
            ?? "unknown";
    }
}
