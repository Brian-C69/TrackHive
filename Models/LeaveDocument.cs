using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class LeaveDocument
{
    public int Id { get; set; }

    [Required]
    public int LeaveRequestId { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }

    [Required]
    [StringLength(256)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [StringLength(260)]
    public string StoredFilePath { get; set; } = string.Empty;

    [StringLength(128)]
    public string? ContentType { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
