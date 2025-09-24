// File: Services/BillingService.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using TrackHive.Models;

namespace TrackHive.Services;

public sealed class BillingService
{
    private const string BillingCurrency = "usd";
    private const string BillingInterval = "month";

    private static readonly IReadOnlyDictionary<SubscriptionPlan, long> PlanAmounts =
        new Dictionary<SubscriptionPlan, long>
        {
            [SubscriptionPlan.Starter] = 99_00L,
            [SubscriptionPlan.Pro] = 199_00L,
            [SubscriptionPlan.Enterprise] = 399_00L
        };

    private readonly StripeOptions _options;
    private readonly SessionService _sessionService;
    private readonly SubscriptionService _subscriptionService;

    private readonly ILogger<BillingService> _logger;

    public BillingService(
        IOptions<StripeOptions> optionsAccessor,
        ILogger<BillingService> logger)

    {
        _options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        StripeConfiguration.ApiKey = _options.SecretKey;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
                CreateSubscriptionLineItem(plan)
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

    private SessionLineItemOptions CreateSubscriptionLineItem(SubscriptionPlan plan)
    {
        if (!PlanAmounts.TryGetValue(plan, out var amount))
        {
            throw new InvalidOperationException($"No pricing configured for plan '{plan}'.");
        }

        var displayName = plan.GetDisplayName();

        return new SessionLineItemOptions
        {
            Quantity = 1,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = BillingCurrency,
                UnitAmount = amount,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = displayName
                },
                Recurring = new SessionLineItemPriceDataRecurringOptions
                {
                    Interval = BillingInterval
                }
            }
        };
    }
}
