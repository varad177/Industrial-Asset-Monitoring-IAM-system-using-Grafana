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
}
