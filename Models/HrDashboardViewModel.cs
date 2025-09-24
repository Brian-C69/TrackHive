namespace TrackHive.Models;

public sealed class HrDashboardViewModel
{
    public required string OrganizationName { get; init; }
    public required SubscriptionPlan CurrentPlan { get; init; }
    public required DateTime BillingPeriodStartUtc { get; init; }
    public DateTime? CurrentPeriodEndsUtc { get; init; }
    public DateTime? TrialEndsUtc { get; init; }
    public InviteEmployeeViewModel Invite { get; init; } = new();
    public IReadOnlyList<LeaveRequestReviewViewModel> PendingLeaveRequests { get; init; } = Array.Empty<LeaveRequestReviewViewModel>();
    public IReadOnlyList<LeaveCertificateReviewViewModel> PendingCertificateRequests { get; init; } = Array.Empty<LeaveCertificateReviewViewModel>();
    public IReadOnlyList<LeaveBalanceSummaryViewModel> LeaveSummaries { get; init; } = Array.Empty<LeaveBalanceSummaryViewModel>();
    public IReadOnlyList<DashboardNotificationViewModel> Notifications { get; init; } = Array.Empty<DashboardNotificationViewModel>();
    public DashboardMetricsViewModel Metrics { get; init; } = new();
    public OrganizationPlan Plan { get; init; } = OrganizationPlan.Free;
    public bool CanViewAnalytics { get; init; }
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

public sealed class LeaveCertificateReviewViewModel
{
    public required int RequestId { get; init; }
    public required string EmployeeName { get; init; }
    public required LeaveType Type { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int TotalDays { get; init; }
    public string? Reason { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
    public IReadOnlyList<LeaveDocumentViewModel> Documents { get; init; } = Array.Empty<LeaveDocumentViewModel>();
}

public sealed class LeaveBalanceSummaryViewModel
{
    public required string EmployeeName { get; init; }
    public required int AnnualEntitlement { get; init; }
    public required int UsedDays { get; init; }
    public required int PendingDays { get; init; }
    public int AvailableDays => Math.Max(0, AnnualEntitlement - UsedDays - PendingDays);
}

public sealed class DashboardNotificationViewModel
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Category { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed class DashboardMetricsViewModel
{
    public int TotalEmployees { get; init; }
    public int PendingLeaveApprovals { get; init; }
    public int PendingCertificateReviews { get; init; }
    public int AwaitingEmployeeCertificates { get; init; }
    public int LeavesReviewedThisMonth { get; init; }
    public IReadOnlyList<MonthlyLeaveTrendViewModel> LeaveTrends { get; init; } = Array.Empty<MonthlyLeaveTrendViewModel>();
    public IReadOnlyList<LeaveTypeBreakdownViewModel> LeaveTypeBreakdown { get; init; } = Array.Empty<LeaveTypeBreakdownViewModel>();
}

public sealed class MonthlyLeaveTrendViewModel
{
    public required string MonthLabel { get; init; }
    public int Pending { get; init; }
    public int Approved { get; init; }
    public int Rejected { get; init; }
}

public sealed class LeaveTypeBreakdownViewModel
{
    public required string Type { get; init; }
    public int Count { get; init; }
}

