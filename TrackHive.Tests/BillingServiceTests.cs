using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TrackHive.Models;
using TrackHive.Services;
using Stripe.Checkout;

namespace TrackHive.Tests;

[TestClass]
public class BillingServiceTests
{
    [DataTestMethod]
    [DataRow(SubscriptionPlan.Starter, 99_00L, "Starter")]
    [DataRow(SubscriptionPlan.Pro, 199_00L, "Pro")]
    [DataRow(SubscriptionPlan.Enterprise, 399_00L, "Enterprise")]
    public void CreateSubscriptionLineItem_ConfiguresExpectedPricing(
        SubscriptionPlan plan,
        long expectedAmount,
        string expectedName)
    {
        var service = CreateService();

        var lineItem = InvokeCreateSubscriptionLineItem(service, plan);

        Assert.AreEqual(1, lineItem.Quantity);
        Assert.IsNotNull(lineItem.PriceData);
        Assert.AreEqual("usd", lineItem.PriceData.Currency);
        Assert.AreEqual(expectedAmount, lineItem.PriceData.UnitAmount);
        Assert.IsNotNull(lineItem.PriceData.ProductData);
        Assert.AreEqual(expectedName, lineItem.PriceData.ProductData.Name);
        Assert.IsNotNull(lineItem.PriceData.Recurring);
        Assert.AreEqual("month", lineItem.PriceData.Recurring.Interval);
    }

    [TestMethod]
    public void CreateSubscriptionLineItem_ThrowsForUnsupportedPlan()
    {
        var service = CreateService();

        Assert.ThrowsException<InvalidOperationException>(
            () => InvokeCreateSubscriptionLineItem(service, SubscriptionPlan.Free));
    }

    private static BillingService CreateService()
    {
        var options = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_123"
        });

        return new BillingService(options, NullLogger<BillingService>.Instance);
    }

    private static SessionLineItemOptions InvokeCreateSubscriptionLineItem(
        BillingService service,
        SubscriptionPlan plan)
    {
        var method = typeof(BillingService).GetMethod(
            "CreateSubscriptionLineItem",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateSubscriptionLineItem method not found.");

        try
        {
            return method.Invoke(service, new object[] { plan }) as SessionLineItemOptions
                ?? throw new InvalidOperationException("CreateSubscriptionLineItem did not return a SessionLineItemOptions.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
