// File: Models/EditUserViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class EditUserViewModel
{
    [Required]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public RoleType Role { get; set; } // display only
}
