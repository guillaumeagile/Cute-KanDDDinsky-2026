using OmniProduct_CoreDomain.Models;

namespace OmniProduct_CoreDomain.Services;

public class SupplierService
{
    private readonly List<Supplier> _suppliers = new();
    private readonly List<ProductSuppliers> _productSuppliers = new();

    public Supplier AddSupplier(string name, string email, string region)
    {
        var supplier = new Supplier
        {
            Id = Ulid.NewUlid(),
            Name = name,
            Email = email,
            Region = region
        };
        _suppliers.Add(supplier);
        return supplier;
    }

    public ProductSuppliers AddSupplierAssignment(Ulid productId, Dictionary<string, Ulid> suppliersRegions)
    {
        var productSuppliers = new ProductSuppliers(productId, suppliersRegions);
        _productSuppliers.Add(productSuppliers);
        return productSuppliers;
    }

    public ProductSuppliers GetSuppliers(Ulid productId)
    {
        var productSuppliers = _productSuppliers.FirstOrDefault(s => s.ProductId == productId);
        if (productSuppliers == null)
            throw new Exception($"No supplier assignment found for product {productId}");
        return productSuppliers;
    }

    public Supplier GetSupplier(Ulid supplierId)
    {
        var supplier = _suppliers.FirstOrDefault(s => s.Id == supplierId);
        if (supplier == null)
            throw new Exception($"No supplier found with id {supplierId}");
        return supplier;
    }

    public Supplier FindSupplierForRegion(string region)
    {
        var supplier = _suppliers.FirstOrDefault(s => s.Region == region);
        if (supplier == null)
            throw new Exception($"No supplier found for region {region}");
        return supplier;
    }

    public void AddSupplierToRegion(Ulid productId, string region)
    {
        GetSuppliers(productId).AddSupplierToRegion(region, FindSupplierForRegion(region).Id);
    }
}
