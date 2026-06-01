using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace IAM.API.Middleware;

public class GrafanaProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _grafanaInternalUrl;

    public GrafanaProxyMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _grafanaInternalUrl = configuration["Grafana:InternalUrl"] ?? "http://iam-grafana:3000";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only proxy requests that start with /grafana
        if (!context.Request.Path.StartsWithSegments("/grafana"))
        {
            await _next(context);
            return;
        }

        var client = _httpClientFactory.CreateClient("GrafanaProxy");
        
        var requestMessage = new HttpRequestMessage();
        var requestMethod = context.Request.Method;
        if (!HttpMethods.IsGet(requestMethod) &&
            !HttpMethods.IsHead(requestMethod) &&
            !HttpMethods.IsDelete(requestMethod) &&
            !HttpMethods.IsTrace(requestMethod))
        {
            var streamContent = new StreamContent(context.Request.Body);
            requestMessage.Content = streamContent;
        }

        foreach (var header in context.Request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        requestMessage.Method = new HttpMethod(context.Request.Method);
        
        // Strip the "/grafana" prefix when forwarding to Grafana internally if needed, or keep it. 
        // Grafana root_url is /grafana/ and serve_from_sub_path = true, so we pass the path as is.
        requestMessage.RequestUri = new Uri($"{_grafanaInternalUrl}{context.Request.Path}{context.Request.QueryString}");
        requestMessage.Headers.Host = "localhost"; // or actual host
        
        // Inject X-JWT-Assertion from cookie
        if (context.Request.Cookies.TryGetValue("iam_access_token", out var token))
        {
            requestMessage.Headers.Remove("X-JWT-Assertion");
            requestMessage.Headers.Add("X-JWT-Assertion", token);
        }

        using var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

        context.Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");

        await responseMessage.Content.CopyToAsync(context.Response.Body);
    }
}
