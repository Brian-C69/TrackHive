using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TrackHive.Models;

public sealed class AdminUserFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public RoleType Role { get; set; } = RoleType.Employee;

    [Display(Name = "Organization")]
    [Required(ErrorMessage = "Please choose an organization.")]
    public int OrganizationId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Must change password on next sign-in")]
    public bool MustChangePassword { get; set; }

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The password confirmation does not match.")]
    public string? ConfirmPassword { get; set; }

    public List<SelectListItem> Organizations { get; set; } = new();
}
