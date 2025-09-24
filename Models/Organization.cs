// File: Models/Organization.cs
using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public enum SubscriptionPlan
{
    Free,
    Starter,
    Pro,
    Enterprise
}

public sealed class Organization
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string CreatedByEmail { get; set; } = string.Empty;

    [Required]
    public OrganizationPlan Plan { get; set; } = OrganizationPlan.Free;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]

    public SubscriptionPlan CurrentPlan { get; set; } = SubscriptionPlan.Free;

    public DateTime BillingPeriodStartUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CurrentPeriodEndsUtc { get; set; }

    public DateTime? TrialEndsUtc { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.Free;


    [StringLength(200)]
    public string? StripeCustomerId { get; set; }

    [StringLength(200)]
    public string? StripeSubscriptionId { get; set; }

    public DateTime? SubscriptionRenewsAt { get; set; }

    public DateTime? SubscriptionUpdatedAt { get; set; }


}
