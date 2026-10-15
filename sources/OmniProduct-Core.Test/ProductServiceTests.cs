using AwesomeAssertions;
using OmniProduct_CoreDomain.Models;
using OmniProduct_CoreDomain.Services;

namespace OmniProduct_Core.Test;

public class ProductServiceTests
{
    // Weak test #1: tests that a constructor sets a property.
    // Not wrong, but tells us nothing about domain behaviour.
    [Fact]
    public void Product_Name_ShouldBeSet()
    {
        var product = new Product(
            id: Ulid.NewUlid(),
            name: "Super Widget",
            slug: "super-widget"
        );

        product.Name.Should().Be("Super Widget");
    }

    // Weak test #2: tests that adding a supplier to the service increments a list.
    // Asserts internal plumbing, not business intent.
    [Fact]
    public void AddSupplier_ShouldReturnSupplierWithCorrectRegion()
    {
        var supplierService = new SupplierService();

        var supplier = supplierService.AddSupplier("Acme", "acme@example.com", "FR");

        supplier.Region.Should().Be("FR");
    }

    // Too-broad test: one test, full lifecycle, asserting everything everywhere.
    // Passes today, impossible to diagnose when it fails.
    [Fact]
    public void FullProductLifecycle_ShouldWork()
    {
        var pricingService = new PricingService();
        var supplierService = new SupplierService();
        var storageService = new StorageService();
        var notificationService = new NotificationService();
        var productLifecycleService = new ProductLifecycleService(pricingService, supplierService, storageService, notificationService);
        var catalogService = new CatalogService(productLifecycleService, supplierService);

        var supplier = supplierService.AddSupplier("Acme", "acme@example.com", "FR");
        var warehouse = storageService.AddWarehouse("Paris Hub", "1 rue de la Paix", "FR");

        var product = catalogService.CreateListing("Super Widget", "FR", 100m, "EUR");
        var storedProduct = storageService.GetStock(product.Id);

        product.Should().NotBeNull();
        product.Name.Should().Be("Super Widget");
        storedProduct.Stock.Should().Be(0);
        product.Status.Should().Be("active");

        storageService.ReceiveStock(product.Id, 50);
        storedProduct.Stock.Should().Be(50);

        productLifecycleService.SellProduct(product.Id, 10);
        storedProduct.Stock.Should().Be(40);
        product.Status.Should().Be("active");

        productLifecycleService.SellProduct(product.Id, 40);
        storedProduct.Stock.Should().Be(0);
        product.Status.Should().Be("out_of_stock");

        var resellerPrice = pricingService.GetResellerPrice(product.Id);
        resellerPrice.Should().Be(124m); // 100 + 20% margin + 20% VAT on margin

        productLifecycleService.DeprecateProduct(product.Id);
        product.Status.Should().Be("deprecated");
        storedProduct.Stock.Should().Be(0);

        var catalog = catalogService.GetCatalog("FR");
        catalog.Should().NotContain(p => p.Id == product.Id);
    }
}
