import { useState, useCallback } from "react";
import { authService } from "../services/authService";

const USER_KEY = "iam_user";

export const useAuth = () => {
  const [user, setUser] = useState(() => {
    try { return JSON.parse(localStorage.getItem(USER_KEY) || "null"); }
    catch { return null; }
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

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