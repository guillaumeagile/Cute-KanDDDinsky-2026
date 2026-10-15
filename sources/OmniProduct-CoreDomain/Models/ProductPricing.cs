using OmniProduct_CoreDomain.Abstractions;
using OmniProduct_CoreDomain.ValueObjects;

namespace OmniProduct_CoreDomain.Models;

// Pricing concern for a Product: the Price value and margin operations, keyed back to the product by ProductId.
public class ProductPricing : IDentifiable
{
    public Ulid ProductId { get; set; }

    public Price Price { get; set; }

    public ProductPricing(Ulid productId, Price price)
    {
        ProductId = productId;
        Price = price;
    }

    public decimal GetResellerPrice()
    {
        return Price.GetResellerPrice();
    }

    public void SetMargin(decimal marginPercent) => Price = Price with { Margin = marginPercent };

    public Ulid Id { get; set; }
}
