const BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5000/api";

const handle = async (res) => {
  const data = await res.json();
  if (!res.ok) throw data;
  return data;
};

export const authService = {
  register: (payload) =>
    fetch(`${BASE_URL}/auth/register`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include", // Send cookies with request
      body: JSON.stringify(payload),
    }).then(handle),

  login: (payload) =>
    fetch(`${BASE_URL}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include", // Send cookies with request
      body: JSON.stringify(payload),
    }).then(handle),

  logout: () =>
    fetch(`${BASE_URL}/auth/logout`, {
      method: "POST",
      credentials: "include",
    }).then(handle),

  refresh: (token) =>
    fetch(`${BASE_URL}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ token }),
    }).then(handle),

  me: (accessToken) =>
    fetch(`${BASE_URL}/auth/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      credentials: "include",
    }).then(handle),

  // Get secure Grafana dashboard URL
  getGrafanaDashboardUrl: (dashboardUid, theme = "dark", timezone = "browser") =>
    fetch(`${BASE_URL}/grafanaembed/dashboard-url?dashboardUid=${dashboardUid}&theme=${theme}&timezone=${timezone}`, {
      headers: { "Content-Type": "application/json" },
      credentials: "include", // Send JWT cookie with request
    }).then(handle),

  // Check Grafana auth status
  checkGrafanaAuthStatus: () =>
    fetch(`${BASE_URL}/grafanaembed/auth-status`, {
      credentials: "include",
    }).then(handle),
};