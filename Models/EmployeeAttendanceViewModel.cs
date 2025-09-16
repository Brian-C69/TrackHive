using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class EmployeeAttendanceViewModel
{
    public required string OrganizationName { get; init; }
    public required string UserName { get; init; }
    public AttendanceDayViewModel? Today { get; init; }
    public IReadOnlyList<AttendanceDayViewModel> RecentRecords { get; init; } = Array.Empty<AttendanceDayViewModel>();
    public bool CanCheckIn { get; init; }
    public bool CanCheckOut { get; init; }
    public LeaveBalanceViewModel LeaveBalance { get; init; } = new();
    public IReadOnlyList<LeaveRequestListItemViewModel> LeaveRequests { get; init; } = Array.Empty<LeaveRequestListItemViewModel>();
    public ApplyLeaveViewModel LeaveApplication { get; init; } = new();
}

public sealed class AttendanceDayViewModel
{
    public required DateOnly Date { get; init; }
    public DateTimeOffset? CheckInTime { get; init; }
    public DateTimeOffset? CheckOutTime { get; init; }
}

public sealed class LeaveBalanceViewModel
{
    public int AnnualEntitlement { get; init; }
    public int UsedDays { get; init; }
    public int PendingDays { get; init; }
    public int RemainingDays => Math.Max(0, AnnualEntitlement - UsedDays - PendingDays);
}

public sealed class LeaveRequestListItemViewModel
{
    public required int Id { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int TotalDays { get; init; }
    public required LeaveRequestStatus Status { get; init; }
    public string? Reason { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public string? ReviewedByName { get; init; }
}

public sealed class ApplyLeaveViewModel
{
    [Required(ErrorMessage = "Start date is required.")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "End date is required.")]
    public DateOnly? EndDate { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

