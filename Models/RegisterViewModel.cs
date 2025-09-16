// File: Models/RegisterViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class RegisterViewModel
{
    [Required, Display(Name = "Organization Name"), StringLength(200)]
    public string OrganizationName { get; set; } = string.Empty;

    [Required, Display(Name = "IT Admin Name"), StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "IT Admin Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
}