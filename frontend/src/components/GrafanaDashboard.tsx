import React, { useState, useEffect } from 'react';
import { authService } from '../services/authService';

/**
 * Props for the GrafanaDashboard component
 */
interface GrafanaDashboardProps {
  /** The dashboard UID (can be found in Grafana dashboard settings) */
  dashboardUid: string;
  
  /** Optional custom title for the dashboard */
  title?: string;
  
  /** Optional height for the iframe (default: 600px) */
  height?: number;
  
  /** Optional theme: 'light' or 'dark' (default: 'dark') */
  theme?: 'light' | 'dark';
  
  /** Optional timezone for the dashboard */
  timezone?: string;
}

/**
 * GrafanaDashboard Component (Secure Cookie-Based)
 * 
 * Securely embeds a Grafana dashboard in an iframe with JWT authentication.
 * 
 * SECURITY IMPROVEMENTS:
 * - JWT token is stored in HTTP-only cookies (not accessible via JavaScript)
 * - Token is NOT exposed in the URL
 * - Dashboard URL is fetched from the backend
 * - Each request validates the user's session server-side
 * 
 * Usage:
 * ```tsx
 * <GrafanaDashboard
 *   dashboardUid="abc123"
 *   title="Asset Monitoring Dashboard"
 *   theme="dark"
 * />
 * ```
 */
export const GrafanaDashboard: React.FC<GrafanaDashboardProps> = ({
  dashboardUid,
  title = 'Dashboard',
  height = 600,
  theme = 'dark',
  timezone = 'browser',
}) => {
  const [iframeUrl, setIframeUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Validate required props
  useEffect(() => {
    if (!dashboardUid) {
      setError('Dashboard UID is required');
      setLoading(false);
      return;
    }

    // Fetch the secure dashboard URL from backend
    const fetchDashboardUrl = async () => {
      try {
        setLoading(true);
        setError(null);

        const response = await authService.getGrafanaDashboardUrl(
          dashboardUid,
          theme,
          timezone
        );

        if (response.dashboardUrl) {
          setIframeUrl(response.dashboardUrl);
        } else {
          setError('Failed to generate dashboard URL');
        }
      } catch (err: any) {
        console.error('Error fetching dashboard URL:', err);
        if (err.message === 'Unauthorized') {
          setError('Your session has expired. Please log in again.');
        } else {
          setError(err.message || 'Failed to load dashboard. Please try again.');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardUrl();
  }, [dashboardUid, theme, timezone]);

  if (error) {
    return (
      <div
        style={{
          padding: '20px',
          backgroundColor: '#f8d7da',
          color: '#721c24',
          borderRadius: '4px',
          marginBottom: '20px',
          border: '1px solid #f5c6cb',
        }}
      >
        <strong>Error:</strong> {error}
      </div>
    );
  }

  if (loading) {
    return (
      <div
        style={{
          padding: '20px',
          backgroundColor: '#e2e3e5',
          color: '#383d41',
          borderRadius: '4px',
          marginBottom: '20px',
          border: '1px solid #d6d8db',
          textAlign: 'center',
        }}
      >
        <strong>Loading dashboard...</strong>
      </div>
    );
  }

  if (!iframeUrl) {
    return (
      <div
        style={{
          padding: '20px',
          backgroundColor: '#f8d7da',
          color: '#721c24',
          borderRadius: '4px',
          marginBottom: '20px',
        }}
      >
        <strong>Error:</strong> Failed to load dashboard URL.
      </div>
    );
  }

  return (
    <div
      style={{
        width: '100%',
        marginBottom: '20px',
      }}
    >
      <div
        style={{
          marginBottom: '10px',
          fontSize: '18px',
          fontWeight: 'bold',
          color: '#333',
        }}
      >
        {title}
      </div>
      <iframe
        src={iframeUrl}
        title={title}
        width="100%"
        height={height}
        frameBorder="0"
        style={{
          border: '1px solid #ddd',
          borderRadius: '4px',
        }}
        sandbox="allow-same-origin allow-scripts allow-presentation allow-popups allow-popups-to-escape-sandbox"
      />
      <div style={{ fontSize: '12px', color: '#666', marginTop: '8px' }}>
        Authentication: Secure (HTTP-only cookies, no token exposure)
      </div>
    </div>
  );
};

export default GrafanaDashboard;
