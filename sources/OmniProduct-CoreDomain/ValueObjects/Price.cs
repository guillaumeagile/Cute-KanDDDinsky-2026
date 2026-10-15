namespace OmniProduct_CoreDomain.ValueObjects;

public record Price
{
    public decimal Amount { get;   }

    public string Currency { get;  }

    public decimal Margin { get; internal init; }     // percentage

    public decimal Vat { get;   }        // percentage, applied on margin only

    public Price(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "A price cannot be negative");

        Amount = amount;
        Currency = currency;
        Margin = 20;
        Vat = 20;
    }

    public decimal GetResellerPrice()
    {
        var marginAmount = Amount * Margin / 100;
        var vatAmount = marginAmount * Vat / 100;
        return Amount + marginAmount + vatAmount;
    }
}
