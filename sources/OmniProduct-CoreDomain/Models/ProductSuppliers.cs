using OmniProduct_CoreDomain.Abstractions;

namespace OmniProduct_CoreDomain.Models;

// Supplier concern for a Product: which supplier serves which region, keyed back to the product by ProductId.
public class ProductSuppliers : IDentifiable
{
    public Ulid ProductId { get; set; }

    public Dictionary<string, Ulid> SuppliersRegions { get; set; } // key = region, value = supplier id

    public ProductSuppliers(Ulid productId, Dictionary<string, Ulid> suppliersRegions)
    {
        ProductId = productId;
        SuppliersRegions = suppliersRegions;
    }

    public void AddSupplierToRegion(string region, Ulid supplierId)
    {
        SuppliersRegions[region] = supplierId;
    }

    public Ulid Id { get; set; }
}
