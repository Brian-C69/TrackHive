using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public enum LeaveType
{
    Annual   = 0,
    Sick     = 1,
    Emergency= 2,
    Unpaid   = 3,
    Other    = 4
}

public enum LeaveRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    ApprovedAwaitingCertificate = 3,
    AwaitingCertificateReview   = 4,
    CertificateRejected         = 5
}

public static class LeaveTypeExtensions
{
    public static bool RequiresMedicalCertificate(this LeaveType type) => type == LeaveType.Sick;
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

    public LeaveType Type { get; set; } = LeaveType.Annual;

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedAt { get; set; }

    public int? ReviewedById { get; set; }

    public AppUser? ReviewedBy { get; set; }

    public ICollection<LeaveDocument> Documents { get; set; } = new List<LeaveDocument>();
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

