using OmniProduct_CoreDomain.Models;
using OmniProduct_CoreDomain.Models.Storage;

namespace OmniProduct_CoreDomain.Services;

public class StorageService
{
    private readonly List<Warehouse> _warehouses = new();
    private readonly List<StoredProduct> _storedProducts = new();

    public Warehouse AddWarehouse(string name, string address, string region)
    {
        var warehouse = new Warehouse
        {
            Id = Ulid.NewUlid(),
            Name = name,
            Address = address,
            Region = region
        };
        _warehouses.Add(warehouse);
        return warehouse;
    }

    public Warehouse FindWarehouseNear(string region)
    {
        var warehouse = _warehouses.FirstOrDefault(w => w.Region == region);
        if (warehouse == null)
            throw new Exception($"No warehouse found for region {region}");
        return warehouse;
    }

    public StoredProduct AddStock(Ulid productId, Ulid warehouseId)
    {
        var storedProduct = new StoredProduct(
            productId: productId,
            weight: 0,
            dimensions: "",
            stock: 0,
            warehouseId: warehouseId
        );
        _storedProducts.Add(storedProduct);
        return storedProduct;
    }

    public StoredProduct GetStock(Ulid productId)
    {
        var storedProduct = _storedProducts.FirstOrDefault(sp => sp.ProductId == productId);
        if (storedProduct == null)
            throw new Exception($"No stock record found for product {productId}");
        return storedProduct;
    }

    public void ReceiveStock(Ulid productId, int quantity) => GetStock(productId).Receive(quantity);
}
