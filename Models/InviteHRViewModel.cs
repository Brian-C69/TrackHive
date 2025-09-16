// File: Models/InviteHRViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class InviteHRViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}