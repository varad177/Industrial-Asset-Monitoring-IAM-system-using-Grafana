import React, { useEffect, useState } from 'react';
import GrafanaDashboard from '../components/GrafanaDashboard';
import { authService } from '../services/authService';

/**
 * Example page that integrates Grafana dashboards
 * Demonstrates secure JWT authentication flow (via HTTP-only cookies)
 */
export const DashboardPage: React.FC = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Verify user is authenticated by checking with backend
  useEffect(() => {
    const checkAuthentication = async () => {
      try {
        // Call backend to verify auth status
        // The JWT cookie will be automatically sent with the request
        const response = await authService.checkGrafanaAuthStatus();
        
        if (response.authenticated) {
          setIsAuthenticated(true);
          setError(null);
        } else {
          setError('Not authenticated. Please log in first.');
        }
      } catch (err) {
        console.error('Auth check failed:', err);
        setError('Session expired. Please log in again.');
      } finally {
        setLoading(false);
      }
    };

    checkAuthentication();
  }, []);

  if (loading) {
    return <div style={{ padding: '20px', textAlign: 'center' }}>Loading...</div>;
  }

  if (error && !isAuthenticated) {
    return (
      <div
        style={{
          padding: '20px',
          backgroundColor: '#f8d7da',
          color: '#721c24',
          borderRadius: '4px',
        }}
      >
        <strong>Authentication Error:</strong> {error}
      </div>
    );
  }

  return (
    <div style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
      <h1>Asset Monitoring Dashboards</h1>
      <p>
        These dashboards are secured with JWT authentication via HTTP-only cookies. 
        Your token is never exposed in the URL.
      </p>

      {/* Main Metrics Dashboard */}
      {isAuthenticated && (
        <GrafanaDashboard
          dashboardUid="iam-asset-telemetry" // Use the actual dashboard UID
          title="Asset Telemetry"
          height={600}
          theme="dark"
          timezone="browser"
        />
      )}

      {/* Real-time Data Dashboard */}
      {/* Commented out - add more dashboards as they are created in Grafana */}
      {/* 
      {isAuthenticated && (
        <GrafanaDashboard
          dashboardUid="real-time-metrics" // Replace with your actual dashboard UID
          title="Real-time Metrics"
          height={500}
          theme="dark"
          timezone="browser"
        />
      )}
      */}

      {/* Alerts Dashboard */}
      {/* Commented out - add more dashboards as they are created in Grafana */}
      {/* 
      {jwtToken && (
        <GrafanaDashboard
          dashboardUid="alerts-dashboard" // Replace with your actual dashboard UID
          title="Active Alerts"
          jwtToken={jwtToken}
          grafanaUrl="http://localhost:3000/grafana"
          height={400}
          theme="dark"
        />
      )}
      */}
    </div>
  );
};

export default DashboardPage;
