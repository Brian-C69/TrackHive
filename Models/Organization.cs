// File: Models/Organization.cs
using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class Organization
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string CreatedByEmail { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.Free;
}
