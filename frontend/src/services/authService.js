import apiClient from "./apiClient";

const BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5500/api";

export const authService = {
  register: (payload) =>
    apiClient(`${BASE_URL}/auth/register`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  login: (payload) =>
    apiClient(`${BASE_URL}/auth/login`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  logout: () =>
    apiClient(`${BASE_URL}/auth/logout`, {
      method: "POST",
    }),

  refresh: (token) =>
    apiClient(`${BASE_URL}/auth/refresh`, {
      method: "POST",
      body: JSON.stringify({ token }),
    }),

  me: (accessToken) =>
    apiClient(`${BASE_URL}/auth/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })
};