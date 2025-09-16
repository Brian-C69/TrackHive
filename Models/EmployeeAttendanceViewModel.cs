namespace TrackHive.Models;

public sealed class EmployeeAttendanceViewModel
{
    public required string OrganizationName { get; init; }
    public required string UserName { get; init; }
    public AttendanceDayViewModel? Today { get; init; }
    public IReadOnlyList<AttendanceDayViewModel> RecentRecords { get; init; } = Array.Empty<AttendanceDayViewModel>();
    public bool CanCheckIn { get; init; }
    public bool CanCheckOut { get; init; }
}

public sealed class AttendanceDayViewModel
{
    public required DateOnly Date { get; init; }
    public DateTimeOffset? CheckInTime { get; init; }
    public DateTimeOffset? CheckOutTime { get; init; }
}
