using System;
using System.Collections.Generic;
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
    public async Task ResolvePriceIdAsync_ReturnsConfiguredId_WhenValueAlreadyPriceId()
    {

        var fakeLookup = new FakePriceLookupService();
        fakeLookup.PriceExistence["price_123"] = true;
        var service = CreateService("price_123", fakeLookup);


        var priceId = await InvokeResolvePriceIdAsync(service, SubscriptionPlan.Starter);

        Assert.AreEqual("price_123", priceId);
        Assert.AreEqual(1, fakeLookup.PriceExistsCallCount);
        Assert.AreEqual(0, fakeLookup.LookupCallCount);

    }

    [TestMethod]
    public async Task ResolvePriceIdAsync_UsesLookupService_ForLookupKeys()
    {
        var fakeLookup = new FakePriceLookupService();
        fakeLookup.LookupResults["starter-monthly"] = "price_456";
        var service = CreateService("starter-monthly", fakeLookup);

        var first = await InvokeResolvePriceIdAsync(service, SubscriptionPlan.Starter);
        var second = await InvokeResolvePriceIdAsync(service, SubscriptionPlan.Starter);

        Assert.AreEqual("price_456", first);
        Assert.AreEqual("price_456", second);
        Assert.AreEqual(1, fakeLookup.LookupCallCount, "Lookup key should be cached after the first resolution.");

    }

    [TestMethod]
    public async Task ResolvePriceIdAsync_Throws_WhenLookupKeyMissing()
    {
        var fakeLookup = new FakePriceLookupService();
        var service = CreateService("missing-key", fakeLookup);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => InvokeResolvePriceIdAsync(service, SubscriptionPlan.Starter));
    }

    private static BillingService CreateService(string starterValue, IStripePriceLookupService lookupService)
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


        return new BillingService(options, lookupService, NullLogger<BillingService>.Instance);

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

    private sealed class FakePriceLookupService : IStripePriceLookupService
    {
        public Dictionary<string, string?> LookupResults { get; } = new();
        public Dictionary<string, bool> PriceExistence { get; } = new();

        public int LookupCallCount { get; private set; }
        public int PriceExistsCallCount { get; private set; }

        public Task<bool> PriceExistsAsync(string priceId)
        {
            PriceExistsCallCount++;
            return Task.FromResult(PriceExistence.TryGetValue(priceId, out var exists) && exists);
        }

        public Task<string?> GetPriceIdByLookupKeyAsync(string lookupKey)
        {
            LookupCallCount++;
            LookupResults.TryGetValue(lookupKey, out var value);
            return Task.FromResult(value);
        }
    }
}
