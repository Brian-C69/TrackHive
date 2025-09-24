// File: Controllers/BillingController.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using TrackHive.Models;
using BillingService = TrackHive.Services.BillingService;

namespace TrackHive.Controllers;

[Authorize(Roles = "IT")]
public sealed class BillingController : Controller
{
    private readonly AppDbContext _db;
    private readonly BillingService _billing;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<BillingController> _logger;

    public BillingController(
        AppDbContext db,
        BillingService billing,
        IOptions<StripeOptions> stripeOptions,
        ILogger<BillingController> logger)
    {
        _db = db;
        _billing = billing;
        _stripeOptions = stripeOptions?.Value ?? new StripeOptions();
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Upgrade()
    {
        var orgId = GetOrgId();
        if (orgId <= 0)
        {
            TempData["Error"] = "We couldn't determine your organization.";
            return RedirectToAction("Index", "Dashboard");
        }

        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId);
        if (org is null)
        {
            TempData["Error"] = "Organization not found.";
            return RedirectToAction("Index", "Dashboard");
        }

        var plans = new List<BillingPlanOption>
        {
            CreatePlanOption(SubscriptionPlan.Starter),
            CreatePlanOption(SubscriptionPlan.Pro),
            CreatePlanOption(SubscriptionPlan.Enterprise)
        };

        var vm = new BillingUpgradeViewModel
        {
            CurrentPlan = org.SubscriptionPlan,
            Plans = plans
        };

        ViewData["Title"] = "Upgrade plan";
        ViewData["OrgName"] = org.Name;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCheckout(SubscriptionPlan plan)
    {
        if (plan is SubscriptionPlan.Free || plan is not (SubscriptionPlan.Starter or SubscriptionPlan.Pro or SubscriptionPlan.Enterprise))
        {
            TempData["Error"] = "Select a paid plan to continue.";
            return RedirectToAction(nameof(Upgrade));
        }

        var orgId = GetOrgId();
        if (orgId <= 0)
        {
            TempData["Error"] = "We couldn't determine your organization.";
            return RedirectToAction(nameof(Upgrade));
        }

        var org = await _db.Organizations.FindAsync(orgId);
        if (org is null)
        {
            TempData["Error"] = "Organization not found.";
            return RedirectToAction(nameof(Upgrade));
        }

        try
        {
            var successUrl = Url.Action(nameof(Success), "Billing", values: null, protocol: Request.Scheme, host: Request.Host.ToString());
            if (string.IsNullOrWhiteSpace(successUrl))
            {
                throw new InvalidOperationException("Unable to resolve the success URL.");
            }
            successUrl = AppendCheckoutSessionId(successUrl);

            var cancelUrl = Url.Action(nameof(Failed), "Billing", new { plan }, Request.Scheme, Request.Host.ToString());
            if (string.IsNullOrWhiteSpace(cancelUrl))
            {
                throw new InvalidOperationException("Unable to resolve the cancel URL.");
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            var session = await _billing.CreateCheckoutSessionAsync(org.Id, plan, email, successUrl, cancelUrl);

            if (string.IsNullOrWhiteSpace(session.Url))
            {
                throw new InvalidOperationException("Stripe did not return a checkout URL.");
            }

            return Redirect(session.Url);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to create Stripe checkout session for organization {OrgId}", orgId);
            var reason = ex.StripeError?.Message ?? ex.Message;
            TempData["Error"] = $"We couldn't start the checkout session. {reason}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start billing upgrade for organization {OrgId}", orgId);
            TempData["Error"] = $"Something went wrong while starting the checkout: {ex.Message}";
        }

        return RedirectToAction(nameof(Upgrade));
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost]
    public async Task<IActionResult> Webhook()
    {
        if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
        {
            _logger.LogWarning("Stripe webhook secret is not configured.");
            return BadRequest();
        }

        string payload;
        using (var reader = new StreamReader(Request.Body))
        {
            payload = await reader.ReadToEndAsync();
        }

        var signature = Request.Headers["Stripe-Signature"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _stripeOptions.WebhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate Stripe webhook signature.");
            return BadRequest();
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case EventTypes.CheckoutSessionCompleted:
                    if (stripeEvent.Data.Object is Session checkoutSession)
                    {
                        await HandleCheckoutSessionAsync(checkoutSession);
                    }
                    break;
                case EventTypes.CustomerSubscriptionCreated:
                case EventTypes.CustomerSubscriptionUpdated:
                    if (stripeEvent.Data.Object is Subscription subscription)
                    {
                        await HandleSubscriptionAsync(subscription);
                    }
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing Stripe webhook {Event}", stripeEvent.Type);
            return StatusCode(500);
        }

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Success(string session_id)
    {
        if (string.IsNullOrWhiteSpace(session_id))
        {
            TempData["Error"] = "Missing Stripe checkout session.";
            return RedirectToAction(nameof(Failed));
        }

        var session = await _billing.GetSessionAsync(session_id);
        if (session is null)
        {
            TempData["Error"] = "We couldn't verify the payment session.";
            return RedirectToAction(nameof(Failed));
        }

        Subscription? subscription = session.Subscription as Subscription;
        if (subscription is null && !string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            subscription = await _billing.GetSubscriptionAsync(session.SubscriptionId);
        }

        var metadataOrgId = TryGetOrganizationId(session.Metadata, out var parsedOrgId) ? parsedOrgId : (int?)null;
        var fallbackOrgId = GetOrgId();
        var resolvedOrgId = metadataOrgId ?? (fallbackOrgId > 0 ? fallbackOrgId : (int?)null);

        if (metadataOrgId is null && fallbackOrgId > 0)
        {
            _logger.LogInformation(
                "Checkout session {SessionId} missing organization metadata. Falling back to authenticated organization {OrgId}.",
                session.Id,
                fallbackOrgId);
        }
        else if (metadataOrgId is not null && fallbackOrgId > 0 && metadataOrgId.Value != fallbackOrgId)
        {
            _logger.LogWarning(
                "Checkout session {SessionId} metadata organization {MetadataOrgId} differs from authenticated organization {OrgId}.",
                session.Id,
                metadataOrgId.Value,
                fallbackOrgId);
        }

        if (resolvedOrgId is null)
        {
            TempData["Error"] = "We couldn't determine which organization to update.";
            return RedirectToAction(nameof(Failed));
        }

        var orgSnapshot = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == resolvedOrgId.Value);
        if (orgSnapshot is not null)
        {
            ViewData["OrgName"] = orgSnapshot.Name;
        }

        var fallbackPlan = orgSnapshot?.SubscriptionPlan ?? SubscriptionPlan.Starter;
        var plan = TryGetPlan(session.Metadata, out var parsedPlan)
            ? parsedPlan
            : ResolvePlanFromSubscription(subscription, fallbackPlan);

        DateTime? renewsAt = NormalizeToUtc(GetSubscriptionPeriodEnd(subscription));

        var updateSucceeded = true;
        try
        {
            await UpdateOrganizationSubscriptionAsync(
                resolvedOrgId.Value,
                plan,
                session.CustomerId,
                subscription?.Id ?? session.SubscriptionId,
                renewsAt);
        }
        catch (Exception ex)
        {
            updateSucceeded = false;
            _logger.LogError(ex, "Failed to persist subscription upgrade for organization {OrgId} from session {SessionId}.", resolvedOrgId.Value, session.Id);
            TempData["Warning"] = "Payment succeeded but we couldn't sync your organization automatically. Please contact support.";
        }

        if (updateSucceeded)
        {
            var updatedOrg = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == resolvedOrgId.Value);
            if (updatedOrg is not null)
            {
                plan = updatedOrg.SubscriptionPlan;
                ViewData["OrgName"] = updatedOrg.Name;
                if (updatedOrg.SubscriptionRenewsAt.HasValue)
                {
                    renewsAt = NormalizeToUtc(updatedOrg.SubscriptionRenewsAt);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Organization {OrgId} was not found after processing checkout session {SessionId}.",
                    resolvedOrgId.Value,
                    session.Id);
                TempData["Warning"] = "Payment succeeded but we couldn't sync your organization automatically. Please contact support.";
            }
        }

        var vm = new BillingStatusViewModel
        {
            Plan = plan,
            Success = true,
            Title = $"{plan.GetDisplayName()} plan activated",
            Message = plan.GetTagline(),
            RenewsAt = renewsAt
        };

        ViewData["Title"] = "Payment successful";
        return View(vm);
    }

    [HttpGet]
    public IActionResult Failed(SubscriptionPlan? plan = null)
    {
        var targetPlan = plan is SubscriptionPlan.Free or null ? SubscriptionPlan.Starter : plan!.Value;

        var vm = new BillingStatusViewModel
        {
            Plan = targetPlan,
            Success = false,
            Title = "Checkout canceled",
            Message = $"Your {targetPlan.GetDisplayName()} upgrade wasn't completed. No charges were made.",
            RenewsAt = null
        };

        ViewData["Title"] = "Payment canceled";
        return View(vm);
    }

    private static BillingPlanOption CreatePlanOption(SubscriptionPlan plan) => new()
    {
        Plan = plan,
        Title = plan.GetDisplayName(),
        Price = plan.GetPriceLabel(),
        Tagline = plan.GetTagline(),
        Highlights = plan.GetHighlights()
    };

    internal static string AppendCheckoutSessionId(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL cannot be null or whitespace.", nameof(baseUrl));
        }

        if (baseUrl.Contains("session_id={CHECKOUT_SESSION_ID}", StringComparison.Ordinal))
        {
            return baseUrl;
        }

        var fragmentIndex = baseUrl.IndexOf('#');
        var hasFragment = fragmentIndex >= 0;
        var fragment = hasFragment ? baseUrl.Substring(fragmentIndex) : string.Empty;
        var withoutFragment = hasFragment ? baseUrl[..fragmentIndex] : baseUrl;

        var separator = withoutFragment.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{withoutFragment}{separator}session_id={{CHECKOUT_SESSION_ID}}{fragment}";
    }

    private async Task HandleCheckoutSessionAsync(Session session)
    {
        if (!TryGetOrganizationId(session.Metadata, out var orgId))
        {
            _logger.LogWarning("Checkout session {SessionId} missing organization metadata.", session.Id);
            return;
        }

        Subscription? subscription = null;
        var subscriptionId = session.SubscriptionId ?? (session.Subscription as Subscription)?.Id;
        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            subscription = await _billing.GetSubscriptionAsync(subscriptionId);
        }

        var plan = TryGetPlan(session.Metadata, out var parsedPlan)
            ? parsedPlan
            : ResolvePlanFromSubscription(subscription, SubscriptionPlan.Starter);

        await UpdateOrganizationSubscriptionAsync(
            orgId,
            plan,
            session.CustomerId,
            subscription?.Id ?? subscriptionId,
            GetSubscriptionPeriodEnd(subscription));
    }

    private async Task HandleSubscriptionAsync(Subscription subscription)
    {
        if (!TryGetOrganizationId(subscription.Metadata, out var orgId))
        {
            _logger.LogWarning("Subscription {SubscriptionId} missing organization metadata.", subscription.Id);
            return;
        }

        var plan = TryGetPlan(subscription.Metadata, out var parsedPlan)
            ? parsedPlan
            : ResolvePlanFromSubscription(subscription, SubscriptionPlan.Starter);

        await UpdateOrganizationSubscriptionAsync(
            orgId,
            plan,
            subscription.CustomerId,
            subscription.Id,
            GetSubscriptionPeriodEnd(subscription));
    }

    private async Task UpdateOrganizationSubscriptionAsync(
        int organizationId,
        SubscriptionPlan plan,
        string? customerId,
        string? subscriptionId,
        DateTime? renewsAt)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org is null)
        {
            _logger.LogWarning("Organization {OrgId} not found while processing Stripe webhook.", organizationId);
            return;
        }

        org.SubscriptionPlan = plan;
        org.CurrentPlan = plan;
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            org.StripeCustomerId = customerId;
        }

        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            org.StripeSubscriptionId = subscriptionId;
        }

        org.SubscriptionRenewsAt = NormalizeToUtc(renewsAt);
        org.SubscriptionUpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    private bool TryGetOrganizationId(IReadOnlyDictionary<string, string> metadata, out int organizationId)
    {
        organizationId = 0;
        if (metadata is null)
        {
            return false;
        }

        if (metadata.TryGetValue("organizationId", out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            organizationId = parsed;
            return true;
        }

        return false;
    }

    private bool TryGetPlan(IReadOnlyDictionary<string, string> metadata, out SubscriptionPlan plan)
    {
        plan = SubscriptionPlan.Free;
        if (metadata is null)
        {
            return false;
        }

        if (metadata.TryGetValue("plan", out var value) &&
            Enum.TryParse(value, true, out SubscriptionPlan parsed) &&
            parsed is SubscriptionPlan.Starter or SubscriptionPlan.Pro or SubscriptionPlan.Enterprise)
        {
            plan = parsed;
            return true;
        }

        return false;
    }

    private SubscriptionPlan ResolvePlanFromSubscription(Subscription? subscription, SubscriptionPlan fallback)
    {
        if (subscription is null)
        {
            return fallback;
        }

        var priceId = subscription.Items?.Data?.FirstOrDefault()?.Price?.Id;
        return ResolvePlanFromPriceId(priceId, fallback);
    }

    private SubscriptionPlan ResolvePlanFromPriceId(string? priceId, SubscriptionPlan fallback)
    {
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return fallback;
        }

        if (!string.IsNullOrWhiteSpace(_stripeOptions.Prices.Starter) &&
            string.Equals(priceId, _stripeOptions.Prices.Starter, StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionPlan.Starter;
        }

        if (!string.IsNullOrWhiteSpace(_stripeOptions.Prices.Pro) &&
            string.Equals(priceId, _stripeOptions.Prices.Pro, StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionPlan.Pro;
        }

        if (!string.IsNullOrWhiteSpace(_stripeOptions.Prices.Enterprise) &&
            string.Equals(priceId, _stripeOptions.Prices.Enterprise, StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionPlan.Enterprise;
        }

        return fallback;
    }

    private static DateTime? GetSubscriptionPeriodEnd(Subscription? subscription)
    {
        var item = subscription?.Items?.Data?.FirstOrDefault();
        if (item is null)
        {
            return null;
        }

        var periodEnd = item.CurrentPeriodEnd;
        if (periodEnd <= DateTime.UnixEpoch)
        {
            return null;
        }

        return periodEnd;
    }

    private int GetOrgId()
    {
        var claim = User.FindFirstValue("OrgId");
        if (int.TryParse(claim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orgId))
        {
            return orgId;
        }

        return 0;
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var dt = value.Value;
        if (dt.Kind == DateTimeKind.Unspecified)
        {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
    }
}
