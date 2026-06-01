import { useState, useCallback, useEffect } from "react";
import { authService } from "../services/authService";

const BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5500/api";
const USER_KEY = "iam_user";

export const useAuth = () => {
  const [user, setUser] = useState(() => {
    try { return JSON.parse(localStorage.getItem(USER_KEY) || "null"); }
    catch { return null; }
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // On mount: if we have a cached user, verify + refresh role from server
  useEffect(() => {
    const cached = localStorage.getItem(USER_KEY);
    if (!cached) return; // not logged in, skip

    fetch(`${BASE_URL}/auth/me`, {
      credentials: "include", // sends HTTP-only cookie
    })
      .then((res) => {
        if (!res.ok) {
          // Cookie expired / invalid — log out
          localStorage.removeItem(USER_KEY);
          setUser(null);
          return null;
        }
        return res.json();
      })
      .then((data) => {
        if (!data) return;
        // data = { userId, email, role } (camelCase from ASP.NET Core)
        const parsed = JSON.parse(cached);
        const freshUser = {
          ...parsed,
          role: data.role ?? parsed.role, // always use server role
        };
        localStorage.setItem(USER_KEY, JSON.stringify(freshUser));
        setUser(freshUser);
        console.log("[useAuth] Role refreshed from server:", freshUser.role);
      })
      .catch((err) => {
        console.warn("[useAuth] Could not refresh user from server:", err);
      });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // run once on mount

  const persistUser = (data) => {
    // Store only user info (NOT tokens - they're now in HTTP-only cookies)
    localStorage.setItem(USER_KEY, JSON.stringify(data.user));
    setUser(data.user);
  };

  const login = useCallback(async (emailOrUsername, password) => {
    setLoading(true);
    setError(null);
    try {
      // authService.login will set HTTP-only cookies automatically
      const data = await authService.login({ emailOrUsername, password });
      persistUser(data);
      return data;
    } catch (err) {
      setError(err.title || "Login failed. Please try again.");
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const register = useCallback(async (payload) => {
    setLoading(true);
    setError(null);
    try {
      const data = await authService.register(payload);
      persistUser(data);
      return data;
    } catch (err) {
      setError(err.title || "Registration failed. Please try again.");
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(async () => {
    try {
      // Call backend to clear cookies
      await authService.logout();
    } catch (err) {
      console.error("Logout failed:", err);
    } finally {
      // Clear local storage
      localStorage.removeItem(USER_KEY);
      setUser(null);
    }
  }, []);

  return { 
    user, 
    loading, 
    error, 
    login, 
    register, 
    logout, 
    isAuthenticated: !!user 
  };
};