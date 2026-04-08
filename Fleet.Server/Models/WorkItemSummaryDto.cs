namespace Fleet.Server.Models;

public record WorkItemSummaryDto(int Total, int Active, int Resolved);

/// <summary>
/// Lightweight state counts computed entirely in the database for dashboard metrics.
/// </summary>
public record WorkItemStateCounts(int Total, int Active, int Resolved, int Closed);
