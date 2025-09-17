using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class InviteEmployeeViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(0, 1_000_000, ErrorMessage = "Monthly salary must be positive.")]
    [Display(Name = "Monthly salary (USD)")]
    public decimal MonthlySalary { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}