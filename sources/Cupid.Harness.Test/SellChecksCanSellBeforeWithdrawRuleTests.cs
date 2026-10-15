using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class SellChecksCanSellBeforeWithdrawRuleTests
{
    private readonly SellChecksCanSellBeforeWithdrawRule _rule = new();

    private const string Preamble = """
        using System;
        namespace Sample;
        public class Stock { public void Withdraw(int quantity) { } }
        public class Product { public bool CanSell(DateOnly today) => true; }
        """;

    [Fact]
    public void IsOnlyAWarning() => _rule.Severity.Should().Be(HarnessSeverity.Warning);

    [Fact]
    public void CanSellBeforeWithdraw_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Service
            {
                public void SellProduct(Product product, Stock stock, int quantity)
                {
                    if (!product.CanSell(DateOnly.MinValue)) return;
                    stock.Withdraw(quantity);
                }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void WithdrawWithoutCanSell_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Service
            {
                public void SellProduct(Product product, Stock stock, int quantity) { stock.Withdraw(quantity); }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Withdraw") && v.Message.Contains("CanSell"));
    }

    [Fact]
    public void CanSellAfterWithdraw_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Service
            {
                public void SellProduct(Product product, Stock stock, int quantity)
                {
                    stock.Withdraw(quantity);
                    product.CanSell(DateOnly.MinValue);
                }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle();
    }

    [Fact]
    public void SellProductNeverWithdrawing_IsNotJudged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Service { public void SellProduct(Product product) { } }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void WithdrawInAnotherMethod_IsNotJudged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Service { public void Remove(Stock stock) { stock.Withdraw(1); } }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }
}
