using System.Security.Claims;
using IAM.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IAM.API.Controllers;

/// <summary>
/// Proxy controller for OpenFGA authorization checks.
/// Allows Grafana to check permissions through the backend API
/// instead of calling OpenFGA directly.
/// 
/// Flow: Grafana → Backend (this controller) → OpenFGA (internal Docker network)
/// 
/// This ensures OpenFGA is never exposed publicly — all authorization
/// checks are mediated by the authenticated backend.
/// </summary>
[ApiController]
[Route("api/fga")]
[Authorize]
[Produces("application/json")]
public class FgaProxyController : ControllerBase
{
    private readonly IOpenFgaService _fga;
    private readonly ILogger<FgaProxyController> _logger;

    public FgaProxyController(IOpenFgaService fga, ILogger<FgaProxyController> logger)
    {
        _fga = fga;
        _logger = logger;
    }

    private string? GetTargetUser(string? requestedUser)
    {
        // If this is a machine-to-machine token (OAuth2 Client Credentials from Grafana)
        if (User.HasClaim(c => c.Type == "client_id"))
        {
            return string.IsNullOrWhiteSpace(requestedUser) ? "grafana-system" : requestedUser;
        }

        // Otherwise, it's a normal user token, so use the requested user OR fallback to the token's email
        return requestedUser ?? User.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Check if a user has a specific relation on an object.
    /// Used by Grafana's Infinity datasource for authorization queries.
    /// 
    /// Example: GET /api/fga/check?user=varad@gmail.com&amp;relation=viewer&amp;objectType=asset&amp;objectId=asset_1
    /// Returns: { "allowed": true }
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] string? user,
        [FromQuery] string relation = "viewer",
        [FromQuery] string objectType = "asset",
        [FromQuery] string objectId = "",
        CancellationToken ct = default)
    {
        var targetUser = GetTargetUser(user);

        if (string.IsNullOrWhiteSpace(targetUser))
            return BadRequest(new { message = "User identifier is required." });

        if (string.IsNullOrWhiteSpace(objectId))
            return BadRequest(new { message = "objectId is required." });

        _logger.LogInformation(
            "FGA Proxy Check — user:{User} {Relation} {ObjectType}:{ObjectId}",
            targetUser, relation, objectType, objectId);

        var allowed = await _fga.CheckAsync(targetUser, relation, objectType, objectId, ct);

        return Ok(new { allowed });
    }

    /// <summary>
    /// Grafana-friendly endpoint that returns "true" or "false" as plain text.
    /// Designed for use as a Grafana dashboard variable (hasPermission).
    /// 
    /// Example: GET /api/fga/has-permission?user=varad@gmail.com&amp;assetId=asset_1
    /// Returns: "true" (plain text)
    /// </summary>
    [HttpGet("has-permission")]
    [Produces("text/plain")]
    public async Task<IActionResult> HasPermission(
        [FromQuery] string? user,
        [FromQuery] string assetId = "",
        CancellationToken ct = default)
    {
        var targetUser = GetTargetUser(user);

        if (string.IsNullOrWhiteSpace(targetUser) || string.IsNullOrWhiteSpace(assetId))
        {
            return Content("false", "text/plain");
        }

        _logger.LogInformation(
            "FGA Proxy HasPermission — user:{User} viewer asset:{AssetId}",
            targetUser, assetId);

        var allowed = await _fga.CheckAsync(targetUser, "viewer", "asset", assetId, ct);

        return Content(allowed ? "true" : "false", "text/plain");
    }
}
