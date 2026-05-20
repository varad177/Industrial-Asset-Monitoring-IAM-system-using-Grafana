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
    private readonly RsaKeyService _rsaKeyService;
    private readonly ILogger<AssetAuthController> _logger;

    // Grafana base URL — must point to Nginx which extracts the JWT
    // private const string GrafanaBaseUrl = "http://localhost/grafana";
    private const string TelemetryDashboardUid = "iam-asset-telemetry";

    public AssetAuthController(
        IOpenFgaService fga,
        IAssetConfigurationProvider assetProvider,
        RsaKeyService rsaKeyService,
        ILogger<AssetAuthController> logger)
    {
        _fga = fga;
        _assetProvider = assetProvider;
        _rsaKeyService = rsaKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a short-lived JWT (30 seconds) specifically for Grafana iframe auth.
    /// Even if someone copies the URL, the token expires almost immediately.
    /// Grafana creates a session cookie on first load, so 30s is plenty.
    /// </summary>
    private string GenerateGrafanaJwt(string userId, string email, string name)
    {
        var signingKey = _rsaKeyService.GetPrivateKey();
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("name", name),
            new Claim("roles", "Viewer"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "IAM.API",
            audience: "IAM.ReactClient",
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(60), // Short-lived! Only needs to survive the iframe load + session cookie creation
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Get all assets the current user is authorized to view.
    /// Checks OpenFGA for viewer relation on each asset.
    /// </summary>
    /// <response code="200">List of authorized assets.</response>
    [HttpGet("assets")]
    [ProducesResponseType(typeof(IEnumerable<AuthorizedAssetDto>), StatusCodes.Status200OK)]
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
    /// <param name="assetId">The specific asset the user selected in React.</param>
    /// <param name="from">Grafana time range start (e.g. now-1h). Default: now-30m</param>
    /// <param name="to">Grafana time range end. Default: now</param>
    /// <param name="ct">Cancellation token.</param>
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
    // NGINX will inject JWT header automatically
    var grafanaUrl =
        $"http://localhost/grafana/d/{TelemetryDashboardUid}/{TelemetryDashboardUid}" +
        $"?orgId=1&kiosk" +
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

    
    
    
    
    
    
    /// <summary>
    /// Check if the current user can access a specific asset.
    /// Used for per-asset access validation before any data query.
    /// </summary>
    /// <param name="assetId">Asset to check access for.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("assets/{assetId}/access")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckAssetAccess(string assetId, CancellationToken ct)
    {
        var userId = GetUserId();
        var allowed = await _fga.CheckAsync(userId, "viewer", "asset", assetId, ct);

        if (!allowed)
        {
            _logger.LogWarning("User {UserId} denied access to asset {AssetId}", userId, assetId);
            return Forbid();
        }

        return Ok(new { assetId, access = "granted" });
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
        if (!string.IsNullOrEmpty(email))
            return email;

        // Fallback to NameIdentifier
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "unknown";
    }
}
