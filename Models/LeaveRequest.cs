using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public enum LeaveRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public sealed class LeaveRequest
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public AppUser? User { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [Range(1, 365)]
    public int TotalDays { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedAt { get; set; }

    public int? ReviewedById { get; set; }

    public AppUser? ReviewedBy { get; set; }
}

public sealed class LeaveBalance
{
    public const int DefaultAnnualEntitlement = 20;

    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public AppUser? User { get; set; }

    [Range(1, 365)]
    public int AnnualEntitlement { get; set; } = DefaultAnnualEntitlement;

    [Range(0, 365)]
    public int UsedDays { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

