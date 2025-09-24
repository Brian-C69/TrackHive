// File: Services/StripePriceLookupService.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stripe;

namespace TrackHive.Services;

public interface IStripePriceLookupService
{
    Task<string?> GetPriceIdByLookupKeyAsync(string lookupKey);
}

public sealed class StripePriceLookupService : IStripePriceLookupService
{
    private readonly PriceService _priceService;

    public StripePriceLookupService()
    {
        _priceService = new PriceService();
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
}
