using System.Net.Http.Json;
using System.Text.Json;
using IAM.Application.Configuration;
using IAM.Application.DTOs.Provisioning;
using IAM.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IAM.Infrastructure.Services.OpenFga;

/// <summary>
/// OpenFGA authorization service using the HTTP API directly.
/// Uses a singleton HttpClient — thread-safe and production-ready.
/// Supports team-based RBAC provisioning.
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

    // ══════════════════════════════════════════════════════════
    //  Existing Methods (Check, ListObjects, WriteTuple)
    // ══════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════
    //  New Provisioning Methods (Delete, Read, Team Management)
    // ══════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task DeleteTupleAsync(string user, string relation, string objectType, string objectId, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiUrl}/stores/{_settings.StoreId}/write";

        var body = new
        {
            deletes = new
            {
                tuple_keys = new[]
                {
                    new
                    {
                        user,
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

            _logger.LogInformation("OpenFGA DeleteTuple — {User} ✕ {Relation} ✕ {Type}:{Id}",
                user, relation, objectType, objectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA DeleteTuple failed for {User} {Relation} {Type}:{Id}",
                user, relation, objectType, objectId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TupleDto>> ReadTuplesAsync(string? user, string? relation, string? objectType, string? objectId, CancellationToken ct = default)
    {
        var url = $"{_settings.ApiUrl}/stores/{_settings.StoreId}/read";

        // Build the tuple_key filter — only include non-null fields
        var tupleKey = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(user)) tupleKey["user"] = user;
        if (!string.IsNullOrEmpty(relation)) tupleKey["relation"] = relation;
        if (!string.IsNullOrEmpty(objectType) && !string.IsNullOrEmpty(objectId))
            tupleKey["object"] = $"{objectType}:{objectId}";
        else if (!string.IsNullOrEmpty(objectType))
            tupleKey["object"] = $"{objectType}:";

        var body = new { tuple_key = tupleKey };

        try
        {
            var allTuples = new List<TupleDto>();
            string? continuationToken = null;

            do
            {
                var requestBody = continuationToken != null
                    ? new { tuple_key = tupleKey, continuation_token = continuationToken }
                    : (object)body;

                var response = await _http.PostAsJsonAsync(url, requestBody, ct);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<ReadTuplesResponse>(JsonOpts, ct);

                if (result?.Tuples != null)
                {
                    allTuples.AddRange(result.Tuples.Select(t =>
                        new TupleDto(t.Key.User, t.Key.Relation, t.Key.Object)));
                }

                continuationToken = string.IsNullOrEmpty(result?.ContinuationToken)
                    ? null
                    : result.ContinuationToken;

            } while (continuationToken != null);

            return allTuples;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA ReadTuples failed for filter user={User} relation={Relation} type={Type}",
                user, relation, objectType);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task AddUserToTeamAsync(string userEmail, string teamName, CancellationToken ct = default)
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
                        user = $"user:{userEmail}",
                        relation = "member",
                        @object = $"team:{teamName}"
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("OpenFGA AddUserToTeam — user:{User} → member → team:{Team}",
                userEmail, teamName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA AddUserToTeam failed for user:{User} team:{Team}",
                userEmail, teamName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveUserFromTeamAsync(string userEmail, string teamName, CancellationToken ct = default)
    {
        await DeleteTupleAsync($"user:{userEmail}", "member", "team", teamName, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetTeamMembersAsync(string teamName, CancellationToken ct = default)
    {
        var tuples = await ReadTuplesAsync(null, "member", "team", teamName, ct);

        return tuples
            .Where(t => t.User.StartsWith("user:"))
            .Select(t => t.User.Split(':', 2)[1])
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserTeamsAsync(string userEmail, CancellationToken ct = default)
    {
        var tuples = await ReadTuplesAsync($"user:{userEmail}", "member", "team", null, ct);

        return tuples
            .Where(t => t.Object.StartsWith("team:"))
            .Select(t => t.Object.Split(':', 2)[1])
            .ToList();
    }

    /// <inheritdoc />
    public async Task GrantTeamAccessAsync(string teamName, string relation, string objectType, string objectId, CancellationToken ct = default)
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
                        user = $"team:{teamName}#member",
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

            _logger.LogInformation("OpenFGA GrantTeamAccess — team:{Team}#member → {Relation} → {Type}:{Id}",
                teamName, relation, objectType, objectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFGA GrantTeamAccess failed for team:{Team} {Relation} {Type}:{Id}",
                teamName, relation, objectType, objectId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RevokeTeamAccessAsync(string teamName, string relation, string objectType, string objectId, CancellationToken ct = default)
    {
        await DeleteTupleAsync($"team:{teamName}#member", relation, objectType, objectId, ct);
    }

    // ── Private response DTOs ─────────────────────────────────

    private sealed record CheckResponse(bool Allowed);

    private sealed record ListObjectsResponse(List<string> Objects);

    private sealed record ReadTuplesResponse(
        List<ReadTupleWrapper> Tuples,
        string? ContinuationToken);

    private sealed record ReadTupleWrapper(ReadTupleKey Key);

    private sealed record ReadTupleKey(string User, string Relation, string Object);
}
