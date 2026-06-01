/**
 * API Client with automatic token refresh on 401
 * Handles JWT expiration and automatically retries requests with new token
 */

const BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5500/api";

// Flag to prevent multiple refresh attempts simultaneously
let isRefreshing = false;
let refreshSubscribers = [];

const subscribeToRefresh = (callback) => {
  refreshSubscribers.push(callback);
};

const notifyRefreshSubscribers = () => {
  refreshSubscribers.forEach((callback) => callback());
  refreshSubscribers = [];
};

/**
 * Refresh token and retry failed requests
 */
const refreshAccessToken = async () => {
  try {
    const response = await fetch(`${BASE_URL}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include", // Sends HTTP-only refresh token cookie
    });

    if (!response.ok) {
      throw new Error("Token refresh failed");
    }

    const data = await response.json();
    
    // Store new access token in localStorage (if not using HTTP-only cookie)
    if (data.accessToken) {
      localStorage.setItem("accessToken", data.accessToken);
    }

    isRefreshing = false;
    notifyRefreshSubscribers();
    
    return true;
  } catch (error) {
    console.error("Token refresh failed:", error);
    isRefreshing = false;
    
    // Logout user on refresh failure
    localStorage.removeItem("iam_user");
    localStorage.removeItem("accessToken");
    window.location.href = "/";
    
    return false;
  }
};

/**
 * Main API fetch wrapper with token refresh logic
 */
export const apiClient = async (url, options = {}) => {
  const fullUrl = url.startsWith("http") ? url : `${BASE_URL}${url}`;

  // Add credentials for HTTP-only cookies
  const requestOptions = {
    ...options,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
  };

  // Add access token from localStorage if available
  const token = localStorage.getItem("accessToken");
  if (token) {
    requestOptions.headers.Authorization = `Bearer ${token}`;
  }

  let response = await fetch(fullUrl, requestOptions);

  // Handle 401 - Token expired
  if (response.status === 401) {
    if (!isRefreshing) {
      isRefreshing = true;
      const refreshed = await refreshAccessToken();

      if (refreshed) {
        // Update token in header and retry request
        const newToken = localStorage.getItem("accessToken");
        if (newToken) {
          requestOptions.headers.Authorization = `Bearer ${newToken}`;
        }
        response = await fetch(fullUrl, requestOptions);
      }
    } else {
      // If already refreshing, wait for refresh to complete then retry
      return new Promise((resolve, reject) => {
        subscribeToRefresh(async () => {
          const newToken = localStorage.getItem("accessToken");
          if (newToken) {
            requestOptions.headers.Authorization = `Bearer ${newToken}`;
          }
          const retryResponse = await fetch(fullUrl, requestOptions);
          const data = await retryResponse.json();

          if (!retryResponse.ok) {
            reject(data);
          } else {
            resolve(data);
          }
        });
      });
    }
  }

  // Parse response
  const data = await response.json();

  if (!response.ok) {
    throw data;
  }

  return data;
};

export default apiClient;
