using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class InviteUserViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public RoleType Role { get; set; } // IT: HR or Employee
}
