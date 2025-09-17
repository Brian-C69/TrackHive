namespace TrackHive.Models;

public sealed class HrDashboardViewModel
{
    public required string OrganizationName { get; init; }
    public InviteEmployeeViewModel Invite { get; init; } = new();
    public IReadOnlyList<LeaveRequestReviewViewModel> PendingLeaveRequests { get; init; } = Array.Empty<LeaveRequestReviewViewModel>();
    public IReadOnlyList<LeaveCertificateReviewViewModel> PendingCertificateRequests { get; init; } = Array.Empty<LeaveCertificateReviewViewModel>();
    public IReadOnlyList<LeaveBalanceSummaryViewModel> LeaveSummaries { get; init; } = Array.Empty<LeaveBalanceSummaryViewModel>();
    public IReadOnlyList<DashboardNotificationViewModel> Notifications { get; init; } = Array.Empty<DashboardNotificationViewModel>();
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

