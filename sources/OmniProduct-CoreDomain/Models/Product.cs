using OmniProduct_CoreDomain.Abstractions;

namespace OmniProduct_CoreDomain.Models;

public class Product : IDentifiable
{
    public Ulid Id { get; set; }

    public string Name { get; set; }

    public string Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }


    public Product()
    {
    }

    public Product(Ulid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Status = "active";
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    // --- Sales ---

    // Storage owns the actual stock decrement (see StoredProduct.Withdraw); this only
    // records the sale's effect on the catalog side: status flip.
    public void Sell(int remainingStock)
    {
        UpdatedAt = DateTime.Now;

        if (remainingStock == 0)
            Status = "out_of_stock";
    }

    public bool CanSell(DateTime sellByDate)
    {
        return DateTime.Now <= sellByDate;
    }

    // --- Lifecycle ---

    public void Deprecate()
    {
        Status = "deprecated";
        UpdatedAt = DateTime.Now;
    }
}
