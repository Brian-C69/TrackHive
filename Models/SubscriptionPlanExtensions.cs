// File: Models/SubscriptionPlanExtensions.cs
using System;
using System.Collections.Generic;

namespace TrackHive.Models;

public static class SubscriptionPlanExtensions
{
    private static readonly IReadOnlyDictionary<SubscriptionPlan, SubscriptionPlanCopy> Copy =
        new Dictionary<SubscriptionPlan, SubscriptionPlanCopy>
        {
            [SubscriptionPlan.Free] = new(
                "Free",
                "Launch and explore TrackHive with core features for small teams.",
                "Free forever",
                new[]
                {
                    "Invite up to 3 team members",
                    "Track attendance and leave basics",
                    "Email notifications for key workflows"
                }),
            [SubscriptionPlan.Starter] = new(
                "Starter",
                "Kickstart HR automation for growing teams.",
                "$99/mo",
                new[]
                {
                    "Up to 25 active employees",
                    "Full leave & attendance automation",
                    "Email invites for HR & employees"
                }),
            [SubscriptionPlan.Pro] = new(
                "Pro",
                "Scale operations with advanced analytics and payroll tools.",
                "$199/mo",
                new[]
                {
                    "Unlimited team invites",
                    "Advanced leave + payroll automations",
                    "Priority email support"
                }),
            [SubscriptionPlan.Enterprise] = new(
                "Enterprise",
                "Enterprise-grade compliance and onboarding at scale.",
                "Custom pricing",
                new[]
                {
                    "Dedicated success manager",
                    "SSO + advanced permissions",
                    "Unlimited document storage"
                })
        };

    public static string GetDisplayName(this SubscriptionPlan plan) =>
        Copy.TryGetValue(plan, out var value) ? value.DisplayName : plan.ToString();

    public static string GetTagline(this SubscriptionPlan plan) =>
        Copy.TryGetValue(plan, out var value) ? value.Tagline : string.Empty;

    public static string GetPriceLabel(this SubscriptionPlan plan) =>
        Copy.TryGetValue(plan, out var value) ? value.Price : string.Empty;

    public static IReadOnlyList<string> GetHighlights(this SubscriptionPlan plan) =>
        Copy.TryGetValue(plan, out var value) ? value.Features : Array.Empty<string>();

    private sealed record SubscriptionPlanCopy(string DisplayName, string Tagline, string Price, IReadOnlyList<string> Features);
}
