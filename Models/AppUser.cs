// File: Models/AppUser.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TrackHive.Models;

[Index(nameof(Email), IsUnique = true)]
public sealed class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(512)] // PBKDF2$iter$salt$hash
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public RoleType Role { get; set; }   // <-- ensure set; exists

    [Required]
    public int OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))] // optional; EF conventions handle this
    public Organization? Organization { get; set; } // nullable by design

    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;

    // Lockout tracking (3 failures → lock)
    public int FailedLoginCount { get; set; } = 0;
    public bool IsLocked { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 1_000_000, ErrorMessage = "Monthly salary must be positive.")]
    public decimal MonthlySalary { get; set; }
}
