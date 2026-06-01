using IAM.Application.DTOs.Provisioning;

namespace IAM.Application.Interfaces;

/// <summary>
/// Abstraction for OpenFGA authorization checks.
/// All methods use the user's email/username as the user identifier (user:{id}).
/// </summary>
public interface IOpenFgaService
{
    /// <summary>
    /// Check if a user has a specific relation on an object.
    /// e.g. CheckAsync("varad", "viewer", "asset", "asset_1")
    /// </summary>
    Task<bool> CheckAsync(string userId, string relation, string objectType, string objectId, CancellationToken ct = default);

    /// <summary>
    /// Get all object IDs of a given type that the user has access to.
    /// e.g. GetAllowedObjectsAsync("varad", "viewer", "asset") → ["asset_1", "asset_2"]
    /// Uses ListObjects API — returns only IDs (not the prefix).
    /// </summary>
    Task<IReadOnlyList<string>> GetAllowedObjectsAsync(string userId, string relation, string objectType, CancellationToken ct = default);

    /// <summary>
    /// Write a tuple granting a user a relation on an object.
    /// Useful for seeding permissions from code.
    /// </summary>
    Task WriteTupleAsync(string userId, string relation, string objectType, string objectId, CancellationToken ct = default);

    /// <summary>
    /// Delete a tuple revoking a user's relation on an object.
    /// </summary>
    Task DeleteTupleAsync(string user, string relation, string objectType, string objectId, CancellationToken ct = default);

    /// <summary>
    /// Read tuples matching a filter. Returns raw OpenFGA tuples.
    /// </summary>
    Task<IReadOnlyList<TupleDto>> ReadTuplesAsync(string? user, string? relation, string? objectType, string? objectId, CancellationToken ct = default);

    /// <summary>Add a user to a team (writes user:{email} → member → team:{teamName}).</summary>
    Task AddUserToTeamAsync(string userEmail, string teamName, CancellationToken ct = default);

    /// <summary>Remove a user from a team.</summary>
    Task RemoveUserFromTeamAsync(string userEmail, string teamName, CancellationToken ct = default);

    /// <summary>Get all member emails of a team.</summary>
    Task<IReadOnlyList<string>> GetTeamMembersAsync(string teamName, CancellationToken ct = default);

    /// <summary>Get all team names a user belongs to.</summary>
    Task<IReadOnlyList<string>> GetUserTeamsAsync(string userEmail, CancellationToken ct = default);

    /// <summary>Grant a team viewer access to an asset or dashboard.</summary>
    Task GrantTeamAccessAsync(string teamName, string relation, string objectType, string objectId, CancellationToken ct = default);

    /// <summary>Revoke a team's access from an asset or dashboard.</summary>
    Task RevokeTeamAccessAsync(string teamName, string relation, string objectType, string objectId, CancellationToken ct = default);
}

