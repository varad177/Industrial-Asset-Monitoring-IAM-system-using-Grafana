import { useState, useEffect } from "react";
import { useAuthContext } from "../../App";
import { provisioningService } from "../../services/provisioningService";
import { telemetryService } from "../../services/telemetryService";
import styles from "./AdminPanel.module.css";

export default function AdminPanel() {
  const { user, logout, setPage } = useAuthContext();
  const [activeTab, setActiveTab] = useState("users"); // users | teams | access

  // Alert State
  const [alert, setAlert] = useState(null); // { type: 'success' | 'error', message: '' }

  // ── Tab 1: Users Overview States ──────────────────────────────
  const [users, setUsers] = useState([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [selectedUserPermissions, setSelectedUserPermissions] = useState(null);
  const [permsLoading, setPermsLoading] = useState(false);

  // ── Tab 2: Team Management States ─────────────────────────────
  const [teams, setTeams] = useState([]);
  const [teamsLoading, setTeamsLoading] = useState(false);
  const [newMembers, setNewMembers] = useState({
    admins: "",
    operators: "",
    viewers: "",
  });

  // ── Tab 3: Access Control States ──────────────────────────────
  const [accessRules, setAccessRules] = useState([]);
  const [allAssets, setAllAssets] = useState([]);
  const [accessLoading, setAccessLoading] = useState(false);

  // Helper to show temporary alerts
  const triggerAlert = (type, message) => {
    setAlert({ type, message });
    setTimeout(() => setAlert(null), 5000);
  };

  // ── Loaders ───────────────────────────────────────────────────
  
  const fetchUsers = async () => {
    setUsersLoading(true);
    try {
      const data = await provisioningService.getUsers();
      setUsers(data);
    } catch (err) {
      console.error("Failed to fetch users", err);
      triggerAlert("error", "Failed to load users list.");
    } finally {
      setUsersLoading(false);
    }
  };

  const fetchTeams = async () => {
    setTeamsLoading(true);
    try {
      const data = await provisioningService.getTeams();
      setTeams(data);
    } catch (err) {
      console.error("Failed to fetch teams", err);
      triggerAlert("error", "Failed to load teams list.");
    } finally {
      setTeamsLoading(false);
    }
  };

  const fetchAccessRules = async () => {
    setAccessLoading(true);
    try {
      const [rulesData, assetsData] = await Promise.all([
        provisioningService.getAccessRules(),
        telemetryService.getAllAssets()
      ]);
      setAccessRules(rulesData);
      setAllAssets(assetsData);
    } catch (err) {
      console.error("Failed to fetch access rules", err);
      triggerAlert("error", "Failed to load access control rules.");
    } finally {
      setAccessLoading(false);
    }
  };

  useEffect(() => {
    if (activeTab === "users") fetchUsers();
    if (activeTab === "teams") fetchTeams();
    if (activeTab === "access") {
      fetchAccessRules();
    }
  }, [activeTab]);

  // ── Actions ───────────────────────────────────────────────────

  // User Actions
  const handleRoleChange = async (email, newRole) => {
    try {
      await provisioningService.updateUserRole(email, newRole);
      triggerAlert("success", `Successfully promoted/changed role of ${email} to ${newRole}`);
      fetchUsers();
    } catch (err) {
      console.error("Failed to change role", err);
      triggerAlert("error", err.message || "Failed to update user role.");
    }
  };

  const handleViewPermissions = async (email) => {
    setPermsLoading(true);
    setSelectedUserPermissions(null);
    try {
      const data = await provisioningService.getUserPermissions(email);
      setSelectedUserPermissions(data);
    } catch (err) {
      console.error("Failed to view permissions", err);
      triggerAlert("error", "Failed to retrieve user permissions.");
    } finally {
      setPermsLoading(false);
    }
  };

  // Team Actions
  const handleAddMember = async (teamName) => {
    const email = newMembers[teamName]?.trim();
    if (!email) return;

    try {
      await provisioningService.addUserToTeam(teamName, email);
      triggerAlert("success", `Successfully added ${email} to team ${teamName}`);
      setNewMembers(prev => ({ ...prev, [teamName]: "" }));
      fetchTeams();
    } catch (err) {
      console.error("Failed to add member", err);
      triggerAlert("error", err.message || "Failed to add member to team.");
    }
  };

  const handleRemoveMember = async (teamName, email) => {
    if (!window.confirm(`Are you sure you want to remove ${email} from team ${teamName}?`)) return;

    try {
      await provisioningService.removeUserFromTeam(teamName, email);
      triggerAlert("success", `Successfully removed ${email} from team ${teamName}`);
      fetchTeams();
    } catch (err) {
      console.error("Failed to remove member", err);
      triggerAlert("error", err.message || "Failed to remove member from team.");
    }
  };

  // Access Actions
  const hasAccess = (teamName, objectType, objectId) => {
    return accessRules.some(
      (r) =>
        r.user === `team:${teamName}#member` &&
        r.relation === "viewer" &&
        r.object === `${objectType}:${objectId}`
    );
  };

  const handleToggleAccess = async (teamName, objectType, objectId, currentState) => {
    if (teamName === "admins") return; // Admins always have access, locked in UI
    
    // Optimistic UI update
    const ruleObj = {
      user: `team:${teamName}#member`,
      relation: "viewer",
      object: `${objectType}:${objectId}`
    };

    if (currentState) {
      // Revoke
      setAccessRules(prev => prev.filter(r => !(r.user === ruleObj.user && r.object === ruleObj.object)));
      try {
        await provisioningService.revokeTeamAccess(teamName, objectType, objectId);
        triggerAlert("success", `Revoked access to ${objectId} for team ${teamName}`);
      } catch (err) {
        triggerAlert("error", "Failed to revoke access.");
        fetchAccessRules(); // Rollback
      }
    } else {
      // Grant
      setAccessRules(prev => [...prev, ruleObj]);
      try {
        await provisioningService.grantTeamAccess(teamName, objectType, objectId);
        triggerAlert("success", `Granted access to ${objectId} for team ${teamName}`);
      } catch (err) {
        triggerAlert("error", "Failed to grant access.");
        fetchAccessRules(); // Rollback
      }
    }
  };

  return (
    <div className={styles.layout}>
      {/* ── Sidebar ───────────────────────────────────────────── */}
      <aside className={styles.sidebar}>
        <div className={styles.sidebarTop}>
          {/* Logo */}
          <div className={styles.sideLogo}>
            <svg viewBox="0 0 32 32" fill="none" width="32" height="32">
              <rect width="32" height="32" rx="8" fill="var(--g-500)" />
              <path d="M8 22 L12 14 L16 18 L20 10 L24 16" stroke="white" strokeWidth="2.5"
                strokeLinecap="round" strokeLinejoin="round" />
              <circle cx="24" cy="16" r="2.5" fill="white" />
            </svg>
            <span className={styles.sideLogoText}>IAM Monitor</span>
          </div>

          {/* Navigation Links */}
          <div className={styles.navSection}>
            <div className={styles.navLabel}>Navigation</div>
            <div className={styles.navLinks}>
              <button className={styles.navLink} onClick={() => setPage("dashboard")}>
                📡 Dashboard
              </button>
              <button className={`${styles.navLink} ${styles.navLinkActive}`}>
                ⚙️ Admin Panel
              </button>
            </div>
          </div>
        </div>

        {/* User Card */}
        <div className={styles.sidebarBottom}>
          <div className={styles.userCard}>
            <div className={styles.avatar}>
              {user?.username?.[0]?.toUpperCase() ?? "A"}
            </div>
            <div className={styles.userInfo}>
              <p className={styles.userName}>{user?.username}</p>
              <p className={styles.userRole}>System Administrator</p>
            </div>
          </div>
          <button className={styles.logoutBtn} onClick={logout}>
            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.5" width="16" height="16">
              <path d="M13 5l5 5-5 5M18 10H7M7 3H4a1 1 0 00-1 1v12a1 1 0 001 1h3" strokeLinecap="round" />
            </svg>
            Sign out
          </button>
        </div>
      </aside>

      {/* ── Main Panel Content ─────────────────────────────────── */}
      <main className={styles.main}>
        <header className={styles.header}>
          <h2 className={styles.headerTitle}>Provisioning & Access Admin</h2>
          <p className={styles.headerSub}>Manage user accounts, assign team memberships, and configure role-based access rules.</p>
        </header>

        {/* Alert Banners */}
        {alert && (
          <div className={`${styles.alert} ${alert.type === "success" ? styles.alertSuccess : styles.alertError}`}>
            <span>{alert.message}</span>
            <button className={styles.alertClose} onClick={() => setAlert(null)}>✕</button>
          </div>
        )}

        {/* Tabs Control */}
        <div className={styles.tabsContainer}>
          <button
            className={`${styles.tabBtn} ${activeTab === "users" ? styles.tabBtnActive : ""}`}
            onClick={() => { setActiveTab("users"); setSelectedUserPermissions(null); }}
          >
            👥 User Accounts
          </button>
          <button
            className={`${styles.tabBtn} ${activeTab === "teams" ? styles.tabBtnActive : ""}`}
            onClick={() => { setActiveTab("teams"); setSelectedUserPermissions(null); }}
          >
            🛡️ Team Management
          </button>
          <button
            className={`${styles.tabBtn} ${activeTab === "access" ? styles.tabBtnActive : ""}`}
            onClick={() => { setActiveTab("access"); setSelectedUserPermissions(null); }}
          >
            🔒 Access Control
          </button>
        </div>

        {/* Content Body */}
        <div className={styles.content}>
          
          {/* ────────────────────────────────────────────────────────
             TAB 1: USERS OVERVIEW
             ──────────────────────────────────────────────────────── */}
          {activeTab === "users" && (
            <div>
              {usersLoading ? (
                <div className={styles.loadingContainer}>
                  <div className={styles.spinner} />
                  <p>Loading users list...</p>
                </div>
              ) : (
                <div className={styles.card}>
                  <h3 className={styles.cardTitle}>Registered Users ({users.length})</h3>
                  <div className={styles.tableWrapper}>
                    <table className={styles.table}>
                      <thead>
                        <tr>
                          <th>Username</th>
                          <th>Email</th>
                          <th>System Role</th>
                          <th>Status</th>
                          <th>Registered At</th>
                          <th>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {users.map(u => (
                          <tr key={u.id}>
                            <td style={{ fontWeight: 600 }}>{u.username}</td>
                            <td>{u.email}</td>
                            <td>
                              <select
                                className={styles.roleSelect}
                                value={u.role}
                                onChange={(e) => handleRoleChange(u.email, e.target.value)}
                              >
                                <option value="User">User</option>
                                <option value="Admin">Admin</option>
                              </select>
                            </td>
                            <td>
                              <span className={`${styles.badge} ${u.isActive ? styles.badgeActive : styles.badgeInactive}`}>
                                {u.isActive ? "Active" : "Inactive"}
                              </span>
                            </td>
                            <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                            <td>
                              <button
                                className={`${styles.btn} ${styles.btnOutline}`}
                                onClick={() => handleViewPermissions(u.email)}
                              >
                                View Permissions
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* Selected User effective permissions summary */}
              {permsLoading && (
                <div className={styles.loadingContainer}>
                  <div className={styles.spinner} />
                  <p>Retrieving user effective permissions...</p>
                </div>
              )}

              {selectedUserPermissions && !permsLoading && (
                <div className={styles.permissionSummaryCard}>
                  <div className={styles.permissionSummaryHeader}>
                    <h4 className={styles.permissionSummaryTitle}>
                      Effective Authorization for: {selectedUserPermissions.userEmail}
                    </h4>
                    <button
                      className={styles.permissionCloseBtn}
                      onClick={() => setSelectedUserPermissions(null)}
                    >
                      ✕
                    </button>
                  </div>
                  
                  <div className={styles.permissionsGrid}>
                    <div className={styles.permissionCol}>
                      <h5 className={styles.permissionColTitle}>Team Memberships</h5>
                      {selectedUserPermissions.teams.length === 0 ? (
                        <p className={styles.emptyPermissions}>None</p>
                      ) : (
                        <div className={styles.permissionItems}>
                          {selectedUserPermissions.teams.map(t => (
                            <span key={t} className={styles.permissionChip}>{t}</span>
                          ))}
                        </div>
                      )}
                    </div>

                    <div className={styles.permissionCol}>
                      <h5 className={styles.permissionColTitle}>Authorized Assets</h5>
                      {selectedUserPermissions.assets.length === 0 ? (
                        <p className={styles.emptyPermissions}>None (No direct or team access)</p>
                      ) : (
                        <div className={styles.permissionItems}>
                          {selectedUserPermissions.assets.map(a => (
                            <span key={a} className={`${styles.permissionChip} ${styles.permissionChipMono}`}>{a}</span>
                          ))}
                        </div>
                      )}
                    </div>

                    <div className={styles.permissionCol}>
                      <h5 className={styles.permissionColTitle}>Authorized Dashboards</h5>
                      {selectedUserPermissions.dashboards.length === 0 ? (
                        <p className={styles.emptyPermissions}>None</p>
                      ) : (
                        <div className={styles.permissionItems}>
                          {selectedUserPermissions.dashboards.map(d => (
                            <span key={d} className={`${styles.permissionChip} ${styles.permissionChipMono}`}>{d}</span>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* ────────────────────────────────────────────────────────
             TAB 2: TEAM MANAGEMENT
             ──────────────────────────────────────────────────────── */}
          {activeTab === "teams" && (
            <div>
              {teamsLoading ? (
                <div className={styles.loadingContainer}>
                  <div className={styles.spinner} />
                  <p>Loading teams and members...</p>
                </div>
              ) : (
                <div className={styles.teamsGrid}>
                  {teams.map(team => (
                    <div key={team.teamName} className={styles.teamCard}>
                      <div className={styles.teamHeader}>
                        <h4 className={styles.teamName}>{team.teamName}</h4>
                        <span className={styles.teamMemberCount}>{team.members.length} members</span>
                      </div>
                      
                      <div className={styles.membersList}>
                        {team.members.length === 0 ? (
                          <p className={styles.emptyMembers}>No members in this team.</p>
                        ) : (
                          team.members.map(memberEmail => (
                            <div key={memberEmail} className={styles.memberItem}>
                              <span className={styles.memberEmail} title={memberEmail}>{memberEmail}</span>
                              <button
                                className={styles.removeBtn}
                                onClick={() => handleRemoveMember(team.teamName, memberEmail)}
                                title="Remove member"
                              >
                                ✕
                              </button>
                            </div>
                          ))
                        )}
                      </div>

                      <div className={styles.addMemberForm}>
                        <input
                          type="email"
                          placeholder="User email"
                          className={styles.addMemberInput}
                          value={newMembers[team.teamName] || ""}
                          onChange={(e) => setNewMembers(prev => ({
                            ...prev,
                            [team.teamName]: e.target.value
                          }))}
                        />
                        <button
                          className={styles.addMemberBtn}
                          onClick={() => handleAddMember(team.teamName)}
                        >
                          Add
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* ────────────────────────────────────────────────────────
             TAB 3: ACCESS CONTROL RULES
             ──────────────────────────────────────────────────────── */}
          {activeTab === "access" && (
            <div>
              {accessLoading ? (
                <div className={styles.loadingContainer}>
                  <div className={styles.spinner} />
                  <p>Loading permission matrix...</p>
                </div>
              ) : (
                <div className={styles.card}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
                    <h3 className={styles.cardTitle} style={{ margin: 0 }}>Permission Matrix</h3>
                    <span style={{ fontSize: "12px", color: "var(--n-400)" }}>Toggle switches to instantly grant or revoke team access</span>
                  </div>
                  
                  <div className={styles.matrixWrapper}>
                    <table className={styles.matrixTable}>
                      <thead>
                        <tr>
                          <th className={styles.matrixRowHeader}>Team</th>
                          {allAssets.map(a => (
                            <th key={a.assetId} className={styles.matrixHeader}>
                              <div className={styles.matrixHeaderContent}>
                                <span className={styles.matrixHeaderTitle}>{a.assetName}</span>
                                <span className={styles.matrixHeaderSub}>{a.assetId}</span>
                              </div>
                            </th>
                          ))}
                          <th className={styles.matrixHeader}>
                            <div className={styles.matrixHeaderContent}>
                              <span className={styles.matrixHeaderTitle}>Dashboard</span>
                              <span className={styles.matrixHeaderSub}>iam-asset-telemetry</span>
                            </div>
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {["admins", "operators", "viewers"].map(team => (
                          <tr key={team}>
                            <td className={styles.matrixRowHeader}>
                              <span className={styles.matrixTeamName}>{team}</span>
                            </td>
                            {allAssets.map(a => {
                              const access = team === "admins" ? true : hasAccess(team, "asset", a.assetId);
                              return (
                                <td key={a.assetId} className={styles.matrixCell}>
                                  <button 
                                    className={`${styles.toggle} ${access ? styles.toggleOn : styles.toggleOff} ${team === "admins" ? styles.toggleDisabled : ""}`}
                                    onClick={() => handleToggleAccess(team, "asset", a.assetId, access)}
                                    disabled={team === "admins"}
                                    title={team === "admins" ? "System Admins have implicit access" : `Toggle ${team} access to ${a.assetName}`}
                                  >
                                    <div className={styles.toggleKnob} />
                                  </button>
                                </td>
                              );
                            })}
                            {/* Dashboard Column */}
                            <td className={styles.matrixCell}>
                              {(() => {
                                const access = team === "admins" ? true : hasAccess(team, "dashboard", "iam-asset-telemetry");
                                return (
                                  <button 
                                    className={`${styles.toggle} ${access ? styles.toggleOn : styles.toggleOff} ${team === "admins" ? styles.toggleDisabled : ""}`}
                                    onClick={() => handleToggleAccess(team, "dashboard", "iam-asset-telemetry", access)}
                                    disabled={team === "admins"}
                                    title={team === "admins" ? "System Admins have implicit access" : `Toggle ${team} access to Telemetry Dashboard`}
                                  >
                                    <div className={styles.toggleKnob} />
                                  </button>
                                );
                              })()}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}

        </div>
      </main>
    </div>
  );
}
