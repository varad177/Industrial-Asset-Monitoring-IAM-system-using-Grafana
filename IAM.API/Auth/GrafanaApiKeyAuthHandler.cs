using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IAM.API.Auth;

/// <summary>
/// Custom authentication handler that validates the X-Grafana-Api-Key header.
/// When valid, creates a ClaimsPrincipal using the 'user' query parameter as the email claim.
/// This allows Grafana's Infinity datasource to authenticate as a valid user
/// without needing a JWT token.
/// </summary>
public class GrafanaApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "GrafanaApiKey";
    private readonly IConfiguration _configuration;

    public GrafanaApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Only handle requests that have the X-Grafana-Api-Key header
        if (!Request.Headers.TryGetValue("X-Grafana-Api-Key", out var providedKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expectedKey = _configuration["Grafana:ApiKey"];

        if (string.IsNullOrEmpty(expectedKey))
        {
            Logger.LogWarning("Grafana:ApiKey is not configured in settings");
            return Task.FromResult(AuthenticateResult.Fail("API key not configured on server."));
        }

        if (providedKey != expectedKey)
        {
            Logger.LogWarning("Invalid Grafana API key provided");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        // API key is valid — extract user identity from query parameter
        // For simple variable queries (like GetSignals), Grafana might not send the user param.
        // In that case, we default to a system identity.
        var userEmail = Request.Query["user"].ToString();
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            userEmail = "grafana-system";
        }

        // Create a proper ClaimsPrincipal so [Authorize] and User.FindFirst work seamlessly
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, userEmail),
            new Claim(ClaimTypes.Name, userEmail),
            new Claim(ClaimTypes.NameIdentifier, userEmail),
            new Claim("auth_method", "grafana_api_key")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        Logger.LogInformation("Grafana API key authenticated for user: {User}", userEmail);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
