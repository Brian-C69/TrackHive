// File: Services/BillingService.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using TrackHive.Models;

namespace TrackHive.Services;

public sealed class BillingService
{
    private readonly StripeOptions _options;
    private readonly SessionService _sessionService;
    private readonly SubscriptionService _subscriptionService;
    private readonly IReadOnlyDictionary<SubscriptionPlan, string> _priceLookup;

    public BillingService(IOptions<StripeOptions> optionsAccessor)
    {
        _options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        StripeConfiguration.ApiKey = _options.SecretKey;
        _priceLookup = _options.Prices.AsDictionary();
        _sessionService = new SessionService();
        _subscriptionService = new SubscriptionService();
    }

    public async Task<Session> CreateCheckoutSessionAsync(
        int organizationId,
        SubscriptionPlan plan,
        string? email,
        string successUrl,
        string cancelUrl)
    {
        if (!_priceLookup.TryGetValue(plan, out var priceId) || string.IsNullOrWhiteSpace(priceId))
        {
            throw new InvalidOperationException($"Stripe price ID is not configured for plan '{plan}'.");
        }

        var metadata = new Dictionary<string, string>
        {
            ["organizationId"] = organizationId.ToString(CultureInfo.InvariantCulture),
            ["plan"] = plan.ToString()
        };
        var subscriptionMetadata = new Dictionary<string, string>(metadata);

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            ClientReferenceId = organizationId.ToString(CultureInfo.InvariantCulture),
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = subscriptionMetadata
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = priceId,
                    Quantity = 1
                }
            }
        };

        options.PaymentMethodTypes = new List<string> { "card" };

        return await _sessionService.CreateAsync(options);
    }

    public async Task<Session?> GetSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var options = new SessionGetOptions();
        options.AddExpand("subscription");

        try
        {
            return await _sessionService.GetAsync(sessionId, options);
        }
        catch (StripeException)
        {
            return null;
        }
    }

    public async Task<Subscription?> GetSubscriptionAsync(string subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return null;
        }

        try
        {
            return await _subscriptionService.GetAsync(subscriptionId);
        }
        catch (StripeException)
        {
            return null;
        }
    }

    public string GetPublishableKey() => _options.PublishableKey;
}
