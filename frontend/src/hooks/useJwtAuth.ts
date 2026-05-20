import { useState, useCallback, useEffect } from 'react';

/**
 * JWT Token data structure
 */
interface JwtToken {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}

/**
 * Auth state structure
 */
interface AuthState {
  token: JwtToken | null;
  isAuthenticated: boolean;
  user: {
    id: string;
    email: string;
    username: string;
    role: string;
  } | null;
  loading: boolean;
  error: string | null;
}

/**
 * useJwtAuth Hook
 *
 * Manages JWT authentication state and provides methods for login/logout/refresh.
 * Automatically handles token refresh when approaching expiry.
 *
 * Usage:
 * ```tsx
 * const { token, isAuthenticated, login, logout, error } = useJwtAuth();
 * ```
 */
export const useJwtAuth = (apiUrl: string = '/api') => {
  const [auth, setAuth] = useState<AuthState>({
    token: null,
    isAuthenticated: false,
    user: null,
    loading: true,
    error: null,
  });

  // Load token from localStorage on mount
  useEffect(() => {
    const loadStoredToken = () => {
      try {
        const stored = localStorage.getItem('jwtToken');
        if (stored) {
          const token: JwtToken = JSON.parse(stored);
          
          // Check if token is expired
          if (token.expiresAt > Date.now()) {
            setAuth((prev) => ({
              ...prev,
              token,
              isAuthenticated: true,
              loading: false,
            }));
          } else {
            // Token expired, try to refresh
            refreshToken(token.refreshToken);
          }
        } else {
          setAuth((prev) => ({ ...prev, loading: false }));
        }
      } catch (err) {
        console.error('Failed to load stored token:', err);
        setAuth((prev) => ({ ...prev, loading: false }));
      }
    };

    loadStoredToken();
  }, []);

  /**
   * Login with email/username and password
   */
  const login = useCallback(
    async (emailOrUsername: string, password: string) => {
      setAuth((prev) => ({ ...prev, loading: true, error: null }));

      try {
        const response = await fetch(`${apiUrl}/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ emailOrUsername, password }),
        });

        if (!response.ok) {
          throw new Error('Login failed');
        }

        const data = await response.json();
        
        // Parse JWT to extract user info
        const decodedToken = parseJwt(data.accessToken);
        
        const token: JwtToken = {
          accessToken: data.accessToken,
          refreshToken: data.refreshToken,
          expiresAt: decodedToken.exp * 1000, // Convert to milliseconds
        };

        // Store token
        localStorage.setItem('jwtToken', JSON.stringify(token));
        localStorage.setItem('accessToken', data.accessToken);

        setAuth({
          token,
          isAuthenticated: true,
          user: {
            id: decodedToken.sub,
            email: decodedToken.email,
            username: decodedToken.name,
            role: decodedToken.role || decodedToken.roles?.[0] || 'Viewer',
          },
          loading: false,
          error: null,
        });

        return { success: true };
      } catch (err) {
        const error = err instanceof Error ? err.message : 'Login failed';
        setAuth((prev) => ({ ...prev, loading: false, error }));
        return { success: false, error };
      }
    },
    [apiUrl]
  );

  /**
   * Refresh access token using refresh token
   */
  const refreshToken = useCallback(
    async (refreshTokenValue: string) => {
      try {
        const response = await fetch(`${apiUrl}/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token: refreshTokenValue }),
        });

        if (!response.ok) {
          throw new Error('Token refresh failed');
        }

        const data = await response.json();
        const decodedToken = parseJwt(data.accessToken);

        const token: JwtToken = {
          accessToken: data.accessToken,
          refreshToken: data.refreshToken,
          expiresAt: decodedToken.exp * 1000,
        };

        localStorage.setItem('jwtToken', JSON.stringify(token));
        localStorage.setItem('accessToken', data.accessToken);

        setAuth((prev) => ({
          ...prev,
          token,
          isAuthenticated: true,
        }));

        return { success: true };
      } catch (err) {
        console.error('Token refresh failed:', err);
        logout();
        return { success: false };
      }
    },
    [apiUrl]
  );

  /**
   * Logout and clear stored tokens
   */
  const logout = useCallback(() => {
    localStorage.removeItem('jwtToken');
    localStorage.removeItem('accessToken');
    setAuth({
      token: null,
      isAuthenticated: false,
      user: null,
      loading: false,
      error: null,
    });
  }, []);

  /**
   * Get current access token
   */
  const getAccessToken = useCallback(() => {
    return auth.token?.accessToken || null;
  }, [auth.token]);

  /**
   * Check if token is expired or about to expire (within 5 minutes)
   */
  const isTokenExpiringSoon = useCallback(() => {
    if (!auth.token) return false;
    const fiveMinutes = 5 * 60 * 1000;
    return auth.token.expiresAt - Date.now() < fiveMinutes;
  }, [auth.token]);

  // Auto-refresh token if expiring soon
  useEffect(() => {
    if (isTokenExpiringSoon() && auth.token?.refreshToken) {
      refreshToken(auth.token.refreshToken);
    }
  }, [isTokenExpiringSoon, auth.token, refreshToken]);

  return {
    ...auth,
    login,
    logout,
    refreshToken,
    getAccessToken,
    isTokenExpiringSoon,
  };
};

/**
 * Decode JWT token (without verification - for client-side use only)
 */
function parseJwt(token: string) {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch (err) {
    console.error('Failed to parse JWT:', err);
    return null;
  }
}

export default useJwtAuth;
