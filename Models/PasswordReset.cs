using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrackHive.Models;

public sealed class PasswordReset
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public AppUser? User { get; set; }

    [Required, StringLength(128)]
    public string Token { get; set; } = string.Empty; // URL-safe

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(1);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; }

    public bool IsUsed => UsedAt != null || DateTime.UtcNow > ExpiresAt;
}