namespace OmniProduct_CoreDomain.Models;

public class PerishableProduct : Product
{
    public DateTime ExpirationDate { get; set; }
    public DateTime ManufacturedDate { get; set; }

    public PerishableProduct(Ulid id, string name, string slug, DateTime manufacturedDate, DateTime expirationDate)
        : base(id, name, slug)
    {
        if (expirationDate <= manufacturedDate)
            throw new ArgumentException("expiration must be after manufacture");

        ManufacturedDate = manufacturedDate;
        ExpirationDate = expirationDate;
    }

    public bool IsExpired() => DateTime.Now > ExpirationDate;

    public new bool CanSell(DateTime sellByDate)
    {
        return !IsExpired() && base.CanSell(sellByDate);
    }
}

public class FreshFoodProduct : PerishableProduct
{
    public int MaxFridgeDays { get; set; }

    public FreshFoodProduct(Ulid id, string name, string slug, DateTime manufacturedDate, DateTime expirationDate, int maxFridgeDays)
        : base(id, name, slug, manufacturedDate, expirationDate)
    {
        MaxFridgeDays = maxFridgeDays;
    }


    public new bool CanSell(DateTime sellByDate)
    {
        if (sellByDate > DateTime.Now.AddDays(MaxFridgeDays))
            throw new InvalidOperationException("cannot plan a sale beyond the fridge life");

        return base.CanSell(sellByDate);
    }

    public new void Sell(int remainingStock)
    {
        UpdatedAt = DateTime.Now;
    }

    public new void Deprecate()
    {
        throw new NotSupportedException("fresh food is discarded, never deprecated");
    }
}
