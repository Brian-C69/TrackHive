using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TrackHive.Models;

public sealed class ProfileViewModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    [Phone]
    [StringLength(32)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? JobTitle { get; set; }

    [StringLength(256)]
    public string? Address { get; set; }

    [StringLength(1024)]
    public string? About { get; set; }

    public string? ExistingImagePath { get; set; }

    public IFormFile? ProfileImage { get; set; }

    public decimal? CropX { get; set; }
    public decimal? CropY { get; set; }
    public decimal? CropWidth { get; set; }
    public decimal? CropHeight { get; set; }

    public bool RemoveImage { get; set; }
}
