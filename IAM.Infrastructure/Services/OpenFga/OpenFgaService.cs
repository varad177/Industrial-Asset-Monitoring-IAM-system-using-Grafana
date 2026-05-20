using System.Net.Http.Json;
using System.Text.Json;
using IAM.Application.Configuration;
using IAM.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IAM.Infrastructure.Services.OpenFga;

/// <summary>
/// OpenFGA authorization service using the HTTP API directly.
/// Uses a singleton HttpClient — thread-safe and production-ready.
/// </summary>
public sealed class OpenFgaService : IOpenFgaService
{
    private readonly HttpClient _http;
    private readonly OpenFgaSettings _settings;
    private readonly ILogger<OpenFgaService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public OpenFgaService(IHttpClientFactory httpFactory, IOptions<OpenFgaSettings> settings, ILogger<OpenFgaService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _http = httpFactory.CreateClient("OpenFGA");
    }

    /// <inheritdoc />
    public async Task<bool> CheckAsync(string userId, string relation, string objectType, string objectId, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiUrl}/stores/{_settings.StoreId}/check";

        var body = new
        {
            tuple_key = new
            {
                user = $"user:{userId}",
                relation,
                @object = $"{objectType}:{objectId}"
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CheckResponse>(JsonOpts, ct);
            var allowed = result?.Allowed ?? false;

            _logger.LogDebug("OpenFGA Check — user:{User} {Relation} {Type}:{Id} → {Result}",
                userId, relation, objectType, objectId, allowed);

            return allowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA Check failed for user:{User} {Relation} {Type}:{Id}",
                userId, relation, objectType, objectId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllowedObjectsAsync(string userId, string relation, string objectType, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiUrl}/stores/{_settings.StoreId}/list-objects";

        var body = new
        {
            user = $"user:{userId}",
            relation,
            type = objectType
        };

        try
        {
            var response = await _http.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ListObjectsResponse>(JsonOpts, ct);

            // Strip the type prefix: "asset:asset_1" → "asset_1"
            var ids = (result?.Objects ?? [])
                .Select(o => o.Contains(':') ? o.Split(':', 2)[1] : o)
                .ToList();

            _logger.LogInformation("OpenFGA ListObjects — user:{User} can view {Count} {Type}(s): [{Ids}]",
                userId, ids.Count, objectType, string.Join(", ", ids));

            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA ListObjects failed for user:{User} {Relation} {Type}",
                userId, relation, objectType);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task WriteTupleAsync(string userId, string relation, string objectType, string objectId, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiUrl}/stores/{_settings.StoreId}/write";

        var body = new
        {
            writes = new
            {
                tuple_keys = new[]
                {
                    new
                    {
                        user = $"user:{userId}",
                        relation,
                        @object = $"{objectType}:{objectId}"
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("OpenFGA WriteTuple — user:{User} → {Relation} → {Type}:{Id}",
                userId, relation, objectType, objectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA WriteTuple failed for user:{User} {Relation} {Type}:{Id}",
                userId, relation, objectType, objectId);
            throw;
        }
    }

    // ── Private response DTOs ─────────────────────────────────

    private sealed record CheckResponse(bool Allowed);

    private sealed record ListObjectsResponse(List<string> Objects);
}
