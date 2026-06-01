using System.Security.Claims;
using IAM.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace IAM.API.Middleware;

public class GrafanaProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _grafanaInternalUrl;
    private readonly IOpenFgaService _fga;
    private readonly ILogger<GrafanaProxyMiddleware> _logger;

    public GrafanaProxyMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOpenFgaService fga,
        ILogger<GrafanaProxyMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _grafanaInternalUrl =
            configuration["Grafana:InternalUrl"] ?? "http://iam-grafana:3000";

        _fga = fga;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only handle Grafana requests
        if (!context.Request.Path.StartsWithSegments("/grafana"))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";

        //
        // Example:
        // /grafana/d/iam-asset-telemetry/iam-asset-telemetry
        //
        var isDashboardRequest =
            path.Contains("/d/", StringComparison.OrdinalIgnoreCase);

        if (isDashboardRequest)
        {
            var assetId =
                context.Request.Query["var-assetId"].FirstOrDefault();

            var email =
                context.User.FindFirst(ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("email")?.Value;

            _logger.LogInformation(
                "Grafana dashboard request. User={User}, Asset={Asset}",
                email,
                assetId);

            if (string.IsNullOrWhiteSpace(email))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("User not authenticated");
                return;
            }

            if (string.IsNullOrWhiteSpace(assetId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("assetId is required");
                return;
            }

            var allowed = await _fga.CheckAsync(
                email,
                "viewer",
                "asset",
                assetId,
                context.RequestAborted);

            if (!allowed)
            {
                _logger.LogWarning(
                    "OpenFGA denied access. User={User}, Asset={Asset}",
                    email,
                    assetId);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Access denied");
                return;
            }

            _logger.LogInformation(
                "OpenFGA granted access. User={User}, Asset={Asset}",
                email,
                assetId);
        }

        var client = _httpClientFactory.CreateClient("GrafanaProxy");

        var requestMessage = new HttpRequestMessage();

        var requestMethod = context.Request.Method;

        if (!HttpMethods.IsGet(requestMethod) &&
            !HttpMethods.IsHead(requestMethod) &&
            !HttpMethods.IsDelete(requestMethod) &&
            !HttpMethods.IsTrace(requestMethod))
        {
            requestMessage.Content =
                new StreamContent(context.Request.Body);
        }

        foreach (var header in context.Request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(
                    header.Key,
                    header.Value.ToArray()))
            {
                requestMessage.Content?.Headers
                    .TryAddWithoutValidation(
                        header.Key,
                        header.Value.ToArray());
            }
        }

        requestMessage.Method =
            new HttpMethod(context.Request.Method);

        requestMessage.RequestUri =
            new Uri(
                $"{_grafanaInternalUrl}{context.Request.Path}{context.Request.QueryString}");

        requestMessage.Headers.Host = "localhost";

        // Forward JWT to Grafana
        if (context.Request.Cookies.TryGetValue(
                "iam_access_token",
                out var token))
        {
            requestMessage.Headers.Remove("X-JWT-Assertion");
            requestMessage.Headers.Add("X-JWT-Assertion", token);
        }

        using var responseMessage =
            await client.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

        context.Response.StatusCode =
            (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            context.Response.Headers[header.Key] =
                header.Value.ToArray();
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            context.Response.Headers[header.Key] =
                header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");

        await responseMessage.Content.CopyToAsync(
            context.Response.Body);
    }
}