import { useState, useEffect, useCallback, useRef } from "react";
import { useAuthContext } from "../App";
import { telemetryService } from "../services/telemetryService";
import styles from "./Dashboard.module.css";

const TIME_RANGES = [
  { label: "15 min", value: "now-15m" },
  { label: "30 min", value: "now-30m" },
  { label: "1 hr", value: "now-1h" },
  { label: "3 hr", value: "now-3h" },
  { label: "6 hr", value: "now-6h" },
  { label: "24 hr", value: "now-24h" },
];

export default function Dashboard() {
  const { user, logout, setPage } = useAuthContext();

  // ── Asset state ──────────────────────────────────────────────
  const [assets, setAssets] = useState([]);
  const [assetsLoading, setAssetsLoading] = useState(false);
  const [assetsError, setAssetsError] = useState(null);
  const [selectedAssetId, setSelectedAssetId] = useState(null);

  // ── Dashboard / Grafana state ─────────────────────────────────
  const [dashboardUrl, setDashboardUrl] = useState(null);
  const [dashboardLoading, setDashboardLoading] = useState(false);
  const [dashboardError, setDashboardError] = useState(null);

  // ── Time range ───────────────────────────────────────────────
  const [timeRange, setTimeRange] = useState("now-30m");
  const [iframeKey, setIframeKey] = useState(0);
  const iframeRef = useRef(null);

  // ── Load authorized assets on mount ─────────────────────────
  const loadAssets = useCallback(async () => {
    setAssetsLoading(true);
    setAssetsError(null);
    try {
      // JWT is sent via HTTP-only cookie automatically
      const data = await telemetryService.getAuthorizedAssets();
      setAssets(data);
      // Auto-select first asset if available
      if (data.length > 0) {
        setSelectedAssetId(data[0].assetId);
      }
    } catch (err) {
      console.error("Failed to load assets:", err);
      setAssetsError("Could not load assets.");
    } finally {
      setAssetsLoading(false);
    }
  }, []);

  useEffect(() => { loadAssets(); }, [loadAssets]);

  // ── Load Grafana URL whenever asset or time range changes ────
  const loadDashboard = useCallback(async (assetId, from) => {
    if (!assetId) return;
    setDashboardLoading(true);
    setDashboardError(null);
    setDashboardUrl(null);
    try {
      // JWT is sent via HTTP-only cookie automatically
      const data = await telemetryService.getDashboardUrl(assetId, from, "now");
      console.log("Got id:", assetId);
      console.log("Got url:", data.url);
      setDashboardUrl(data.url);
    } catch (err) {
      console.error("Dashboard load error:", err);
      if (err?.status === 403 || err?.title?.includes("403")) {
        setDashboardError("You don't have permission to view this asset's dashboard.");
      } else {
        setDashboardError("Could not load dashboard. Is the backend running?");
      }
    } finally {
      setDashboardLoading(false);
    }
  }, []);

  // Trigger dashboard reload when asset or time range changes
  useEffect(() => {
    if (selectedAssetId) {
      loadDashboard(selectedAssetId, timeRange);
      setIframeKey((k) => k + 1);
    }
  }, [selectedAssetId, timeRange, loadDashboard]);

  const handleAssetSelect = (assetId) => {
    setSelectedAssetId(assetId);
  };

  const handleTimeChange = (newRange) => {
    setTimeRange(newRange);
  };

  const handleRefresh = () => {
    loadDashboard(selectedAssetId, timeRange);
    setIframeKey((k) => k + 1);
  };

  const selectedAsset = assets.find((a) => a.assetId === selectedAssetId);

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

          {/* Navigation (Admin only) */}
          {user?.role?.toLowerCase() === "admin" && (
            <div className={styles.navSection}>
              <div className={styles.navLabel}>Navigation</div>
              <div className={styles.navLinks}>
                <button className={`${styles.navLink} ${styles.navLinkActive}`}>
                  📡 Dashboard
                </button>
                <button className={styles.navLink} onClick={() => setPage("admin")}>
                  ⚙️ Admin Panel
                </button>
              </div>
            </div>
          )}

          {/* Asset Selector — the main filter */}
          <div className={styles.assetSection}>
            <div className={styles.sectionLabel}>
              <span>Select Asset</span>
              {assetsLoading && <span className={styles.spinnerSm} />}
            </div>

            {assetsError && (
              <div className={styles.sideError}>{assetsError}</div>
            )}

            {!assetsLoading && assets.length === 0 && !assetsError && (
              <div className={styles.emptyAssets}>
                <span>🔒</span>
                <p>No assets assigned.<br />Contact your admin.</p>
              </div>
            )}

            <div className={styles.assetList}>
              {assets.map((asset) => (
                <button
                  key={asset.assetId}
                  className={`${styles.assetBtn} ${selectedAssetId === asset.assetId ? styles.assetBtnActive : ""}`}
                  onClick={() => handleAssetSelect(asset.assetId)}
                >
                  <div className={styles.assetBtnLeft}>
                    <span className={`${styles.assetDot} ${selectedAssetId === asset.assetId ? styles.assetDotActive : ""}`} />
                    <div>
                      <div className={styles.assetBtnName}>{asset.assetName}</div>
                      <div className={styles.assetBtnId}>{asset.assetId}</div>
                    </div>
                  </div>
                  <span className={styles.assetBtnSignals}>{asset.signalCount} signals</span>
                </button>
              ))}
            </div>
          </div>

          {/* Selected asset info */}
          {selectedAsset && (
            <div className={styles.selectedInfo}>
              <div className={styles.selectedInfoLabel}>Active Asset</div>
              <div className={styles.selectedInfoName}>{selectedAsset.assetName}</div>
              <div className={styles.selectedInfoMeta}>
                {selectedAsset.signalCount} signals · Signal filter ↗ in Grafana
              </div>
            </div>
          )}
        </div>

        {/* User Card */}
        <div className={styles.sidebarBottom}>
          <div className={styles.userCard}>
            <div className={styles.avatar}>
              {user?.username?.[0]?.toUpperCase() ?? "U"}
            </div>
            <div className={styles.userInfo}>
              <p className={styles.userName}>{user?.username}</p>
              <p className={styles.userRole}>{assets.length} asset{assets.length !== 1 ? "s" : ""} authorized</p>
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

      {/* ── Main ─────────────────────────────────────────────── */}
      <main className={styles.main}>

        {/* Header */}
        <header className={styles.header}>
          <div>
            <h2 className={styles.headerTitle}>
              {selectedAsset ? selectedAsset.assetName : "Select an Asset"}
            </h2>
            <p className={styles.headerSub}>
              {selectedAsset
                ? `${selectedAsset.assetId} · ${selectedAsset.signalCount} signals · Use Grafana signal filter to drill down`
                : "Choose an asset from the sidebar to load its dashboard"}
            </p>
          </div>

          <div className={styles.headerRight}>
            {/* Time Range Pills */}
            <div className={styles.timeRangeBar}>
              <span className={styles.timeRangeLabel}>Range</span>
              <div className={styles.timeRangePills}>
                {TIME_RANGES.map(({ label, value }) => (
                  <button
                    key={value}
                    className={`${styles.timePill} ${timeRange === value ? styles.timePillActive : ""}`}
                    onClick={() => handleTimeChange(value)}
                  >
                    {label}
                  </button>
                ))}
              </div>
            </div>

            <button
              className={styles.refreshBtn}
              onClick={handleRefresh}
              disabled={!selectedAssetId}
              title="Refresh"
            >
              <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" width="16" height="16">
                <path d="M4 4v5h5M16 16v-5h-5" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M4.93 9A8 8 0 1114 4.93" strokeLinecap="round" />
              </svg>
            </button>

            <div className={styles.headerBadge}>
              <span className={styles.dot} /> Live
            </div>
          </div>
        </header>

        {/* Dashboard Area */}
        <div className={styles.grafanaArea}>

          {/* No asset selected yet */}
          {!selectedAssetId && !assetsLoading && assets.length > 0 && (
            <div className={styles.grafanaState}>
              <div className={styles.placeholderIcon}>📡</div>
              <h3>Select an Asset</h3>
              <p>Choose one of your authorized assets from the sidebar<br />to load its live telemetry dashboard.</p>
            </div>
          )}

          {/* No assets at all */}
          {!assetsLoading && assets.length === 0 && !assetsError && (
            <div className={styles.grafanaState}>
              <div className={styles.placeholderIcon}>🔒</div>
              <h3>No Assets Authorized</h3>
              <p>You don't have access to any assets yet.<br />Ask your administrator to grant access.</p>
            </div>
          )}

          {/* Loading dashboard */}
          {selectedAssetId && dashboardLoading && (
            <div className={styles.grafanaState}>
              <div className={styles.loaderRing} />
              <p>Loading {selectedAsset?.assetName ?? selectedAssetId} dashboard…</p>
            </div>
          )}

          {/* Error */}
          {selectedAssetId && !dashboardLoading && dashboardError && (
            <div className={styles.grafanaState}>
              <div className={styles.errorIcon}>⚠️</div>
              <h3>Dashboard Unavailable</h3>
              <p>{dashboardError}</p>
              <button className={styles.retryBtn} onClick={() => loadDashboard(selectedAssetId, timeRange)}>
                Retry
              </button>
            </div>
          )}

          {/* Grafana iframe */}
          {selectedAssetId && !dashboardLoading && !dashboardError && dashboardUrl && (
            <>
              <div className={styles.grafanaHint}>
                <span>📊 Use the <strong>Signals</strong> dropdown inside Grafana to filter which signals are shown</span>
              </div>
              <iframe
                ref={iframeRef}
                key={iframeKey}
                src={dashboardUrl}
                className={styles.grafanaIframe}
                title={`${selectedAsset?.assetName ?? "Asset"} Telemetry Dashboard`}
                frameBorder="0"
                allowFullScreen
              />
            </>
          )}
        </div>
      </main>
    </div>
  );
}