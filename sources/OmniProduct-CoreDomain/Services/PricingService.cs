using OmniProduct_CoreDomain.Models;
using OmniProduct_CoreDomain.ValueObjects;

namespace OmniProduct_CoreDomain.Services;

public class PricingService
{
    private readonly List<ProductPricing> _pricings = new();

    public ProductPricing AddPricing(Ulid productId, Price price)
    {
        var pricing = new ProductPricing(productId, price);
        _pricings.Add(pricing);
        return pricing;
    }

    public ProductPricing GetPricing(Ulid productId)
    {
        var pricing = _pricings.FirstOrDefault(p => p.ProductId == productId);
        if (pricing == null)
            throw new Exception($"No pricing found for product {productId}");
        return pricing;
    }

    public decimal GetResellerPrice(Ulid productId)
    {
        return GetPricing(productId).GetResellerPrice();
    }

    public void SetMargin(Ulid productId, decimal marginPercent)
    {
        GetPricing(productId).SetMargin(marginPercent);
    }
}
