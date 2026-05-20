using IAM.Application.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace IAM.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, title, errors) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "Validation failed",
                (object)ve.Errors),

            UnauthorizedException ue => (
                HttpStatusCode.Unauthorized,
                ue.Message,
                (object)new { }),

            ConflictException ce => (
                HttpStatusCode.Conflict,
                ce.Message,
                (object)new { }),

            NotFoundException nfe => (
                HttpStatusCode.NotFound,
                nfe.Message,
                (object)new { }),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                (object)new { })
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            status = (int)statusCode,
            title,
            errors
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}