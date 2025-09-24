// File: Services/BillingService.cs
using System;
using System.Collections.Concurrent;
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
    private readonly IReadOnlyDictionary<SubscriptionPlan, string> _configuredPrices;
    private readonly IStripePriceLookupService _priceLookupService;
    private readonly ConcurrentDictionary<string, string> _priceIdCache = new(StringComparer.OrdinalIgnoreCase);

    public BillingService(
        IOptions<StripeOptions> optionsAccessor,
        IStripePriceLookupService priceLookupService)
    {
        _options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        StripeConfiguration.ApiKey = _options.SecretKey;
        _configuredPrices = _options.Prices.AsDictionary();
        _priceLookupService = priceLookupService ?? throw new ArgumentNullException(nameof(priceLookupService));
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
        var priceId = await ResolvePriceIdAsync(plan);

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
        options.AddExpand("subscription.items.data.price");

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

        var options = new SubscriptionGetOptions();
        options.AddExpand("items.data.price");

        try
        {
            return await _subscriptionService.GetAsync(subscriptionId, options);
        }
        catch (StripeException)
        {
            return null;
        }
    }

    public string GetPublishableKey() => _options.PublishableKey;

    private async Task<string> ResolvePriceIdAsync(SubscriptionPlan plan)
    {
        if (!_configuredPrices.TryGetValue(plan, out var configuredValue) || string.IsNullOrWhiteSpace(configuredValue))
        {
            throw new InvalidOperationException($"Stripe price ID is not configured for plan '{plan}'.");
        }

        if (configuredValue.StartsWith("price_", StringComparison.OrdinalIgnoreCase))
        {
            return configuredValue;
        }

        if (_priceIdCache.TryGetValue(configuredValue, out var cached))
        {
            return cached;
        }

        var priceId = await _priceLookupService.GetPriceIdByLookupKeyAsync(configuredValue);
        if (string.IsNullOrWhiteSpace(priceId))
        {
            throw new InvalidOperationException($"Stripe price lookup key '{configuredValue}' is not associated with a price.");
        }

        _priceIdCache[configuredValue] = priceId;
        return priceId;
    }
}
