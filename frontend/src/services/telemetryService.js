import apiClient from "./apiClient";

const BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:5500/api";

export const telemetryService = {
  /**
   * Get all assets regardless of authorization (for admin provisioning).
   * Returns: [{ assetId, assetName, signalCount }]
   */
  getAllAssets: () =>
    apiClient(`${BASE_URL}/telemetry/assets`),

  /**
   * Get all assets the user is authorized to view (OpenFGA filtered).
   * Returns: [{ assetId, assetName, signalCount }]
   */
  getAuthorizedAssets: () =>
    apiClient(`${BASE_URL}/assets`),

  /**
   * Get the Grafana iframe URL for ONE specific asset.
   * Backend checks OpenFGA for access to that exact asset.
   * Returns: { url, dashboardUid, allowedAssetIds: [assetId] }
   *
   * @param {string} assetId - The asset the user selected
   * @param {string} from - Grafana time range start (e.g. "now-1h")
   * @param {string} to - Grafana time range end (e.g. "now")
   */
  getDashboardUrl: (assetId, from = "now-30m", to = "now") =>
    apiClient(
      `${BASE_URL}/dashboards/asset_telemetry/url` +
        `?assetId=${encodeURIComponent(assetId)}` +
        `&from=${encodeURIComponent(from)}` +
        `&to=${encodeURIComponent(to)}`
    ),
};
