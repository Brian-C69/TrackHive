// File: Models/StripeOptions.cs
using System.Collections.Generic;

namespace TrackHive.Models;

public sealed class StripeOptions
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional base URL used to build Stripe success/cancel redirects.
    /// When not provided the application falls back to the current HTTP request.
    /// </summary>
    public string? CheckoutRedirectBaseUrl { get; set; }

    public StripePriceOptions Prices { get; set; } = new();

    public sealed class StripePriceOptions
    {
        public string Starter { get; set; } = string.Empty;
        public string Pro { get; set; } = string.Empty;
        public string Enterprise { get; set; } = string.Empty;

        public IReadOnlyDictionary<SubscriptionPlan, string> AsDictionary() => new Dictionary<SubscriptionPlan, string>
        {
            [SubscriptionPlan.Starter] = Starter,
            [SubscriptionPlan.Pro] = Pro,
            [SubscriptionPlan.Enterprise] = Enterprise
        };
    }
}
