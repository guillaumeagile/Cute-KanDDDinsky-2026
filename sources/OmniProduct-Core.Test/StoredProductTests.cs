using AwesomeAssertions;
using OmniProduct_CoreDomain.Models;
using OmniProduct_CoreDomain.Models.Storage;

namespace OmniProduct_Core.Test;

public class StoredProductTests
{
    private static StoredProduct CreateStoredProduct(int stock = 0)
    {
        return new StoredProduct(
            productId: Ulid.NewUlid(),
            weight: 0.5,
            dimensions: "10x5x3",
            stock: stock,
            warehouseId: Ulid.NewUlid()
        );
    }

    [Fact]
    public void Receive_ShouldIncreaseStock()
    {
        var storedProduct = CreateStoredProduct(stock: 10);

        storedProduct.Receive(5);

        storedProduct.Stock.Should().Be(15);
    }

    [Fact]
    public void Withdraw_ShouldDecreaseStock()
    {
        var storedProduct = CreateStoredProduct(stock: 10);

        storedProduct.Withdraw(4);

        storedProduct.Stock.Should().Be(6);
    }

    [Fact]
    public void Withdraw_ExactRemainingStock_ShouldBringStockToZero()
    {
        var storedProduct = CreateStoredProduct(stock: 10);

        storedProduct.Withdraw(10);

        storedProduct.Stock.Should().Be(0);
    }

    [Fact]
    public void Withdraw_MoreThanAvailableStock_ShouldThrow()
    {
        var storedProduct = CreateStoredProduct(stock: 5);

        var act = () => storedProduct.Withdraw(6);

        act.Should().Throw<Exception>().WithMessage("Not enough stock");
        storedProduct.Stock.Should().Be(5);
    }
}
