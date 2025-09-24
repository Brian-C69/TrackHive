// File: Services/StripePriceLookupService.cs
using System.Collections.Generic;
using System.Linq;

using System.Net;

using System.Threading.Tasks;
using Stripe;

namespace TrackHive.Services;

public interface IStripePriceLookupService
{
    Task<bool> PriceExistsAsync(string priceId);

    Task<string?> GetPriceIdByLookupKeyAsync(string lookupKey);
}

public sealed class StripePriceLookupService : IStripePriceLookupService
{
    private readonly PriceService _priceService;

    public StripePriceLookupService()
    {
        _priceService = new PriceService();
    }


    public async Task<bool> PriceExistsAsync(string priceId)
    {
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return false;
        }

        try
        {
            var price = await _priceService.GetAsync(priceId);
            return price is not null;
        }
        catch (StripeException ex) when (IsMissingResource(ex))
        {
            return false;
        }
    }

    public async Task<string?> GetPriceIdByLookupKeyAsync(string lookupKey)
    {
        if (string.IsNullOrWhiteSpace(lookupKey))
        {
            return null;
        }

        var options = new PriceListOptions
        {
            LookupKeys = new List<string> { lookupKey },
            Limit = 1
        };

        var prices = await _priceService.ListAsync(options);
        return prices.Data.FirstOrDefault()?.Id;
    }


    private static bool IsMissingResource(StripeException ex)
    {
        if (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }

        return string.Equals(ex.StripeError?.Code, "resource_missing", System.StringComparison.OrdinalIgnoreCase);
    }

}
