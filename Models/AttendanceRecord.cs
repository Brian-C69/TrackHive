namespace TrackHive.Models;

public sealed class AttendanceRecord
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public DateOnly Date { get; set; }

    public DateTimeOffset? CheckInTime { get; set; }

    public DateTimeOffset? CheckOutTime { get; set; }
}
