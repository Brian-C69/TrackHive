using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TrackHive.Models;
using TrackHive.Services;

namespace TrackHive.Tests;

[TestClass]
public class BillingServiceTests
{
    [TestMethod]
    public async Task ResolvePriceIdAsync_ReturnsConfiguredId_ForConfiguredPlan()
    {
        var service = CreateService("price_123");

        var priceId = await InvokeResolvePriceIdAsync(service, SubscriptionPlan.Starter);

        Assert.AreEqual("price_123", priceId);
    }

    [TestMethod]
    public async Task ResolvePriceIdAsync_ReturnsConfiguredId_ForDifferentPlans()
    {
        var service = CreateService("price_starter");

        var starter = await InvokeResolvePriceIdAsync(service, SubscriptionPlan.Starter);
        var pro = await InvokeResolvePriceIdAsync(service, SubscriptionPlan.Pro);
        var enterprise = await InvokeResolvePriceIdAsync(service, SubscriptionPlan.Enterprise);

        Assert.AreEqual("price_starter", starter);
        Assert.AreEqual("price_pro", pro);
        Assert.AreEqual("price_enterprise", enterprise);
    }

    [TestMethod]
    public async Task ResolvePriceIdAsync_Throws_WhenPriceNotConfigured()
    {
        var service = CreateService(string.Empty);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => InvokeResolvePriceIdAsync(service, SubscriptionPlan.Starter));
    }

    private static BillingService CreateService(string starterValue)
    {
        var options = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_123",
            Prices = new StripeOptions.StripePriceOptions
            {
                Starter = starterValue,
                Pro = "price_pro",
                Enterprise = "price_enterprise"
            }
        });

        return new BillingService(options, NullLogger<BillingService>.Instance);
    }

    private static Task<string> InvokeResolvePriceIdAsync(BillingService service, SubscriptionPlan plan)
    {
        var method = typeof(BillingService).GetMethod(
            "ResolvePriceIdAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolvePriceIdAsync method not found.");

        var task = method.Invoke(service, new object[] { plan }) as Task<string>
            ?? throw new InvalidOperationException("ResolvePriceIdAsync did not return a Task<string>.");

        return task;
    }
}
