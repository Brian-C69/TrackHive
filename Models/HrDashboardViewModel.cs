namespace TrackHive.Models;

public sealed class HrDashboardViewModel
{
    public required string OrganizationName { get; init; }
    public InviteEmployeeViewModel Invite { get; init; } = new();
    public IReadOnlyList<LeaveRequestReviewViewModel> PendingLeaveRequests { get; init; } = Array.Empty<LeaveRequestReviewViewModel>();
    public IReadOnlyList<LeaveBalanceSummaryViewModel> LeaveSummaries { get; init; } = Array.Empty<LeaveBalanceSummaryViewModel>();
}

public sealed class LeaveRequestReviewViewModel
{
    public required int RequestId { get; init; }
    public required string EmployeeName { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int TotalDays { get; init; }
    public string? Reason { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public required int AnnualEntitlement { get; init; }
    public required int AvailableDays { get; init; }
}

public sealed class LeaveBalanceSummaryViewModel
{
    public required string EmployeeName { get; init; }
    public required int AnnualEntitlement { get; init; }
    public required int UsedDays { get; init; }
    public required int PendingDays { get; init; }
    public int AvailableDays => Math.Max(0, AnnualEntitlement - UsedDays - PendingDays);
}

