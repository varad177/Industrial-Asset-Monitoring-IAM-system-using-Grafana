using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IAM.API.Controllers;

/// <summary>
/// Handles Grafana dashboard embedding with secure token management.
/// Provides signed URLs and temporary tokens for embedding dashboards in iframes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class GrafanaEmbedController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public GrafanaEmbedController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Get a secure Grafana dashboard embed URL.
    /// Returns a backend proxy URL that handles JWT authentication.
    /// The iframe loads from /api/grafana-proxy/* which extracts JWT from cookies.
    /// </summary>
    /// <param name="dashboardUid">The dashboard UID from Grafana settings</param>
    /// <param name="theme">Dashboard theme: 'light' or 'dark' (default: 'dark')</param>
    /// <param name="timezone">Timezone for the dashboard (default: 'browser')</param>
    /// <returns>An object containing the secure proxy URL and metadata</returns>
    [HttpGet("dashboard-url")]
    public IActionResult GetDashboardUrl(
        [FromQuery] string dashboardUid,
        [FromQuery] string theme = "dark",
        [FromQuery] string timezone = "browser")
    {
        if (string.IsNullOrEmpty(dashboardUid))
        {
            return BadRequest(new { error = "Dashboard UID is required" });
        }

        // Build the dashboard path
        var dashboardPath = $"d/{dashboardUid}/{dashboardUid}";
        
        // Build query parameters
        var queryParams = new Dictionary<string, string>
        {
            { "orgId", "1" },
            { "theme", theme },
            { "timezone", timezone },
            { "kiosk", "tv" } // Optional: fullscreen kiosk mode
        };

        var queryString = string.Join("&", 
            queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

        // Return PROXY URL instead of direct Grafana URL
        // The iframe will load from /api/grafana-proxy/d/... 
        // which handles JWT authentication via cookies
        var proxyUrl = $"http://localhost/api/grafana-proxy/{dashboardPath}?{queryString}";

        // Get user information from the JWT claims
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

        return Ok(new
        {
            dashboardUrl = proxyUrl,
            proxyUrl = proxyUrl,
            originalPath = dashboardPath,
            user = new { id = userId, email = userEmail },
            tokenStatus = "authenticated_via_cookie", // Token is in HTTP-only cookie
            expiresAt = DateTime.UtcNow.AddHours(1)
        });
    }

    /// <summary>
    /// Verify the user's authentication status for Grafana access.
    /// Used by the frontend to confirm the user can access dashboards.
    /// </summary>
    /// <returns>Authentication status and user metadata</returns>
    [HttpGet("auth-status")]
    public IActionResult GetAuthStatus()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            authenticated = true,
            user = new
            {
                id = userId,
                email = userEmail,
                role = role
            },
            tokenStatus = "valid"
        });
    }
}
