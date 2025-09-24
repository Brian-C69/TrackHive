using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TrackHive.Controllers;

namespace TrackHive.Tests;

[TestClass]
public class BillingControllerTests
{
    [TestMethod]
    public void AppendCheckoutSessionId_AppendsPlaceholder_WhenQueryMissing()
    {
        var baseUrl = "https://example.com/Billing/Success";

        var result = BillingController.AppendCheckoutSessionId(baseUrl);

        Assert.AreEqual("https://example.com/Billing/Success?session_id={CHECKOUT_SESSION_ID}", result);
    }

    [TestMethod]
    public void AppendCheckoutSessionId_AppendsWithAmpersand_WhenQueryExists()
    {
        var baseUrl = "https://example.com/Billing/Success?foo=bar";

        var result = BillingController.AppendCheckoutSessionId(baseUrl);

        Assert.AreEqual("https://example.com/Billing/Success?foo=bar&session_id={CHECKOUT_SESSION_ID}", result);
    }

    [TestMethod]
    public void AppendCheckoutSessionId_DoesNotDuplicatePlaceholder()
    {
        var baseUrl = "https://example.com/Billing/Success?session_id={CHECKOUT_SESSION_ID}";

        var result = BillingController.AppendCheckoutSessionId(baseUrl);

        Assert.AreEqual(baseUrl, result);
    }

    [TestMethod]
    public void AppendCheckoutSessionId_InsertsBeforeFragment()
    {
        var baseUrl = "https://example.com/Billing/Success?foo=bar#section";

        var result = BillingController.AppendCheckoutSessionId(baseUrl);

        Assert.AreEqual("https://example.com/Billing/Success?foo=bar&session_id={CHECKOUT_SESSION_ID}#section", result);
    }

    [TestMethod]
    public void AppendCheckoutSessionId_ThrowsForEmptyUrl()
    {
        Assert.ThrowsException<ArgumentException>(() => BillingController.AppendCheckoutSessionId(" "));
    }
}
