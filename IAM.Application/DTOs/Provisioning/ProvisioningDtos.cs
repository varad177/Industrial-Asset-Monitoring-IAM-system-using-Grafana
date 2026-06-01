namespace IAM.Application.DTOs.Provisioning;

/// <summary>Request to add a user to a team.</summary>
public sealed record AssignUserToTeamRequest(string UserEmail, string TeamName);

/// <summary>Request to remove a user from a team.</summary>
public sealed record RemoveUserFromTeamRequest(string UserEmail, string TeamName);

/// <summary>Request to grant a team access to an asset or dashboard.</summary>
public sealed record GrantTeamAccessRequest(
    string TeamName,
    string ObjectType,
    string ObjectId,
    string Relation = "viewer");

/// <summary>Request to revoke a team's access from an asset or dashboard.</summary>
public sealed record RevokeTeamAccessRequest(
    string TeamName,
    string ObjectType,
    string ObjectId,
    string Relation = "viewer");

/// <summary>An OpenFGA relationship tuple.</summary>
public sealed record TupleDto(string User, string Relation, string Object);

/// <summary>A team with its list of member emails.</summary>
public sealed record TeamDto(string TeamName, IReadOnlyList<string> Members);

/// <summary>Summary of a user's permissions — teams, assets, dashboards.</summary>
public sealed record UserPermissionDto(
    string UserEmail,
    IReadOnlyList<string> Teams,
    IReadOnlyList<string> Assets,
    IReadOnlyList<string> Dashboards);

/// <summary>A registered user summary for admin listing.</summary>
public sealed record UserSummaryDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>Request to update a user's role.</summary>
public sealed record UpdateUserRoleRequest(string Role);
