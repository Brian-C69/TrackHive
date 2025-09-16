using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? InfoMessage { get; set; }
}