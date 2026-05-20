using IAM.Application.DTOs;
using IAM.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IAM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Register a new user account</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Login with email/username and password</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        
        // Determine if we're in development or production
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        
        // Set JWT in HTTP-only secure cookie
        Response.Cookies.Append(
            "iam_access_token",
            result.AccessToken,
            new CookieOptions
            {
                HttpOnly = true,              // Cannot be accessed by JavaScript
                Secure = !isDevelopment,      // HTTPS only in production
                SameSite = SameSiteMode.Lax,  // CSRF protection
                Expires = DateTimeOffset.UtcNow.AddMinutes(60)
            }
        );

        // Set refresh token in HTTP-only cookie (optional, for token rotation)
        Response.Cookies.Append(
            "iam_refresh_token",
            result.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !isDevelopment,      // HTTPS only in production
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            }
        );

        return Ok(result);
    }

    /// <summary>Rotate access token using a valid refresh token</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Get currently authenticated user info</summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        return Ok(new { UserId = userId, Email = email, Role = role });
    }

    /// <summary>Logout and clear authentication cookies</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("iam_access_token");
        Response.Cookies.Delete("iam_refresh_token");
        return Ok(new { message = "Logged out successfully" });
    }
}