namespace IAM.Application.DTOs.Authorization;

/// <summary>Response DTO for an authorized asset.</summary>
public sealed record AuthorizedAssetDto(
    string AssetId,
    string AssetName,
    int SignalCount);

/// <summary>Response DTO for an authorized Grafana dashboard.</summary>
public sealed record AuthorizedDashboardDto(
    string DashboardId,
    string Title,
    string Uid);

/// <summary>Response containing the Grafana iframe URL for a dashboard.</summary>
public sealed record DashboardUrlDto(
    string Url,
    string DashboardUid,
    IReadOnlyList<string> AllowedAssetIds);
