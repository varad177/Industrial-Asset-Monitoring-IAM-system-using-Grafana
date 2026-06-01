using IAM.Application.DTOs.Provisioning;
using IAM.Application.Interfaces;
using IAM.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IAM.API.Controllers;

/// <summary>
/// Provisioning controller for administrator team and access management.
/// Only users with the "Admin" role can access these endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class ProvisioningController : ControllerBase
{
    private readonly IOpenFgaService _fga;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProvisioningController> _logger;

    public ProvisioningController(
        IOpenFgaService fga,
        IUnitOfWork uow,
        ILogger<ProvisioningController> logger)
    {
        _fga = fga;
        _uow = uow;
        _logger = logger;
    }

    /// <summary>Add a user to a team</summary>
    [HttpPost("teams/{teamName}/members")]
    public async Task<IActionResult> AddUserToTeam(string teamName, [FromBody] AssignUserToTeamRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserEmail))
            return BadRequest(new { message = "UserEmail is required." });

        _logger.LogInformation("Adding user {UserEmail} to team {TeamName}", request.UserEmail, teamName);
        await _fga.AddUserToTeamAsync(request.UserEmail, teamName, ct);
        return Ok(new { message = $"User {request.UserEmail} added to team {teamName}" });
    }

    /// <summary>Remove a user from a team</summary>
    [HttpDelete("teams/{teamName}/members/{email}")]
    public async Task<IActionResult> RemoveUserFromTeam(string teamName, string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required." });

        _logger.LogInformation("Removing user {Email} from team {TeamName}", email, teamName);
        await _fga.RemoveUserFromTeamAsync(email, teamName, ct);
        return Ok(new { message = $"User {email} removed from team {teamName}" });
    }

    /// <summary>List team members</summary>
    [HttpGet("teams/{teamName}/members")]
    public async Task<IActionResult> GetTeamMembers(string teamName, CancellationToken ct)
    {
        _logger.LogInformation("Fetching members for team {TeamName}", teamName);
        var members = await _fga.GetTeamMembersAsync(teamName, ct);
        return Ok(members);
    }

    /// <summary>List all teams and their members</summary>
    [HttpGet("teams")]
    public async Task<IActionResult> GetTeams(CancellationToken ct)
    {
        _logger.LogInformation("Fetching all teams and members");
        
        var teamMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "admins", new List<string>() },
            { "operators", new List<string>() },
            { "viewers", new List<string>() }
        };

        foreach (var teamName in teamMap.Keys.ToList())
        {
            var members = await _fga.GetTeamMembersAsync(teamName, ct);
            foreach (var email in members)
            {
                if (!teamMap[teamName].Contains(email))
                {
                    teamMap[teamName].Add(email);
                }
            }
        }

        var result = teamMap.Select(kvp => new TeamDto(kvp.Key, kvp.Value)).ToList();
        return Ok(result);
    }

    /// <summary>List all access control rules (team-to-object mappings)</summary>
    [HttpGet("access")]
    public async Task<IActionResult> GetAccessRules(CancellationToken ct)
    {
        _logger.LogInformation("Fetching all access rules");
        
        var teams = new[] { "admins", "operators", "viewers" };
        var allTuples = new List<TupleDto>();

        foreach (var team in teams)
        {
            var user = $"team:{team}#member";
            try 
            {
                var assetTuples = await _fga.ReadTuplesAsync(user, "viewer", "asset", null, ct);
                allTuples.AddRange(assetTuples);

                var dashboardTuples = await _fga.ReadTuplesAsync(user, "viewer", "dashboard", null, ct);
                allTuples.AddRange(dashboardTuples);
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch access rules for {User}", user);
            }
        }
            
        return Ok(allTuples);
    }

    /// <summary>Grant a team access to an asset or dashboard</summary>
    [HttpPost("teams/{teamName}/access")]
    public async Task<IActionResult> GrantTeamAccess(string teamName, [FromBody] GrantTeamAccessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectType) || string.IsNullOrWhiteSpace(request.ObjectId))
            return BadRequest(new { message = "ObjectType and ObjectId are required." });

        var relation = string.IsNullOrWhiteSpace(request.Relation) ? "viewer" : request.Relation;

        _logger.LogInformation("Granting team {TeamName} access to {ObjectType}:{ObjectId} as {Relation}",
            teamName, request.ObjectType, request.ObjectId, relation);

        await _fga.GrantTeamAccessAsync(teamName, relation, request.ObjectType, request.ObjectId, ct);
        return Ok(new { message = $"Access granted: team {teamName} can {relation} {request.ObjectType}:{request.ObjectId}" });
    }

    /// <summary>Revoke a team's access from an asset or dashboard</summary>
    [HttpDelete("teams/{teamName}/access")]
    public async Task<IActionResult> RevokeTeamAccess(string teamName, [FromBody] RevokeTeamAccessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectType) || string.IsNullOrWhiteSpace(request.ObjectId))
            return BadRequest(new { message = "ObjectType and ObjectId are required." });

        var relation = string.IsNullOrWhiteSpace(request.Relation) ? "viewer" : request.Relation;

        _logger.LogInformation("Revoking team {TeamName} access from {ObjectType}:{ObjectId} as {Relation}",
            teamName, request.ObjectType, request.ObjectId, relation);

        await _fga.RevokeTeamAccessAsync(teamName, relation, request.ObjectType, request.ObjectId, ct);
        return Ok(new { message = $"Access revoked: team {teamName} no longer has {relation} on {request.ObjectType}:{request.ObjectId}" });
    }

    /// <summary>Get a user's full permission summary</summary>
    [HttpGet("users/{email}/permissions")]
    public async Task<IActionResult> GetUserPermissions(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required." });

        _logger.LogInformation("Fetching permission summary for user {Email}", email);
        var teams = await _fga.GetUserTeamsAsync(email, ct);
        var assets = await _fga.GetAllowedObjectsAsync(email, "viewer", "asset", ct);
        var dashboards = await _fga.GetAllowedObjectsAsync(email, "viewer", "dashboard", ct);

        return Ok(new UserPermissionDto(email, teams, assets, dashboards));
    }

    /// <summary>List all registered users</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        _logger.LogInformation("Fetching all registered users");
        var users = await _uow.Users.GetAllAsync(ct);
        var summaries = users.Select(u => new UserSummaryDto(
            u.Id,
            u.Username,
            u.Email,
            u.Role,
            u.IsActive,
            u.CreatedAt
        )).ToList();
        return Ok(summaries);
    }

    /// <summary>Update a user's role</summary>
    [HttpPut("users/{email}/role")]
    public async Task<IActionResult> UpdateUserRole(string email, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required." });
        if (string.IsNullOrWhiteSpace(request.Role))
            return BadRequest(new { message = "Role is required." });

        _logger.LogInformation("Updating role of user {Email} to {Role}", email, request.Role);
        var user = await _uow.Users.GetByEmailAsync(email, ct);
        if (user == null)
            return NotFound(new { message = $"User '{email}' not found." });

        var oldRole = user.Role;
        user.SetRole(request.Role);
        await _uow.SaveChangesAsync(ct);

        // Sync role change with OpenFGA team memberships
        var oldTeamName = oldRole.ToLower() + "s";
        var newTeamName = request.Role.ToLower() + "s";

        if (oldTeamName != newTeamName)
        {
            try
            {
                await _fga.RemoveUserFromTeamAsync(email, oldTeamName, ct);
                _logger.LogInformation("Removed {Email} from old team {TeamName}", email, oldTeamName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove user {Email} from old team {TeamName} (may not exist)", email, oldTeamName);
            }

            try
            {
                await _fga.AddUserToTeamAsync(email, newTeamName, ct);
                _logger.LogInformation("Added {Email} to new team {TeamName}", email, newTeamName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add user {Email} to new team {TeamName}", email, newTeamName);
            }
        }

        return Ok(new { message = $"User {email} role updated to {request.Role} and team synchronized." });
    }
}
