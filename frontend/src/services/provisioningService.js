import apiClient from "./apiClient";

const BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5500/api";

export const provisioningService = {
  /**
   * Get all registered users.
   * Returns: [{ id, username, email, role, isActive, createdAt }]
   */
  getUsers: () =>
    apiClient(`${BASE_URL}/provisioning/users`),

  /**
   * Update a user's role.
   * @param {string} email
   * @param {string} role - "Admin" | "Operator" | "Viewer"
   */
  updateUserRole: (email, role) =>
    apiClient(`${BASE_URL}/provisioning/users/${encodeURIComponent(email)}/role`, {
      method: "PUT",
      body: JSON.stringify({ role }),
    }),

  /**
   * Get a user's full permission summary.
   * Returns: { userEmail, teams: [], assets: [], dashboards: [] }
   */
  getUserPermissions: (email) =>
    apiClient(`${BASE_URL}/provisioning/users/${encodeURIComponent(email)}/permissions`),

  /**
   * Get all teams and their members.
   * Returns: [{ teamName, members: [] }]
   */
  getTeams: () =>
    apiClient(`${BASE_URL}/provisioning/teams`),

  /**
   * Get all team members.
   * @param {string} teamName
   * Returns: [{ email, role }]
   */
  getTeamMembers: (teamName) =>
    apiClient(`${BASE_URL}/provisioning/teams/${encodeURIComponent(teamName)}/members`),

  /**
   * Add a user to a team.
   * @param {string} teamName
   * @param {string} email
   */
  addUserToTeam: (teamName, email) =>
    apiClient(`${BASE_URL}/provisioning/teams/${encodeURIComponent(teamName)}/members`, {
      method: "POST",
      body: JSON.stringify({ userEmail: email, teamName }),
    }),

  /**
   * Remove a user from a team.
   * @param {string} teamName
   * @param {string} email
   */
  removeUserFromTeam: (teamName, email) =>
    apiClient(
      `${BASE_URL}/provisioning/teams/${encodeURIComponent(teamName)}/members/${encodeURIComponent(email)}`,
      {
        method: "DELETE",
      }
    ),

  /**
   * Grant a team access to an asset or dashboard.
   * @param {string} teamName
   * @param {string} objectType - "asset" | "dashboard"
   * @param {string} objectId
   * @param {string} relation - default "viewer"
   */
  grantTeamAccess: (teamName, objectType, objectId, relation = "viewer") =>
    apiClient(`${BASE_URL}/provisioning/teams/${encodeURIComponent(teamName)}/access`, {
      method: "POST",
      body: JSON.stringify({ teamName, objectType, objectId, relation }),
    }),

  /**
   * Revoke a team's access from an asset or dashboard.
   * @param {string} teamName
   * @param {string} objectType - "asset" | "dashboard"
   * @param {string} objectId
   * @param {string} relation - default "viewer"
   */
  revokeTeamAccess: (teamName, objectType, objectId, relation = "viewer") =>
    apiClient(`${BASE_URL}/provisioning/teams/${encodeURIComponent(teamName)}/access`, {
      method: "DELETE",
      body: JSON.stringify({ teamName, objectType, objectId, relation }),
    }),

  /**
   * List all access control rules (team-to-object mappings).
   * Returns: [{ user, relation, object }]
   */
  getAccessRules: () =>
    apiClient(`${BASE_URL}/provisioning/access`),
};

