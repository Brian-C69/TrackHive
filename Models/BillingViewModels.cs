// File: Models/BillingViewModels.cs
using System;
using System.Collections.Generic;

namespace TrackHive.Models;

public sealed class BillingPlanOption
{
    public required SubscriptionPlan Plan { get; init; }
    public required string Title { get; init; }
    public required string Price { get; init; }
    public required string Tagline { get; init; }
    public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();
}

public sealed class BillingUpgradeViewModel
{
    public required IReadOnlyList<BillingPlanOption> Plans { get; init; }
    public SubscriptionPlan CurrentPlan { get; init; }
}

public sealed class BillingStatusViewModel
{
    public required SubscriptionPlan Plan { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public bool Success { get; init; }
    public DateTime? RenewsAt { get; init; }
}
