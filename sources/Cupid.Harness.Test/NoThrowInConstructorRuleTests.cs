using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class NoThrowInConstructorRuleTests
{
    private readonly NoThrowInConstructorRule _rule = new();

    [Fact]
    public void ThrowStatementInConstructor_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            namespace Sample;
            public class Money
            {
                public Money(decimal amount)
                {
                    if (amount < 0) throw new ArgumentException("negative");
                }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Money") && v.Message.Contains("'throw' statement"));
    }

    [Fact]
    public void ThrowExpressionInConstructor_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            namespace Sample;
            public class Money
            {
                public string Currency { get; }
                public Money(string currency) => Currency = currency ?? throw new ArgumentNullException(nameof(currency));
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'throw' expression"));
    }

    [Fact]
    public void ThrowIfGuardInConstructor_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            namespace Sample;
            public record Money
            {
                public Money(string currency) { ArgumentNullException.ThrowIfNull(currency); }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("ThrowIf"));
    }

    [Fact]
    public void ThrowInConstructorOfAnyType_RecordsToo_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            namespace Sample;
            public record Perishable
            {
                public Perishable(DateOnly sellBy, DateOnly useBy)
                {
                    if (sellBy > useBy) throw new InvalidOperationException();
                }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Perishable"));
    }

    [Fact]
    public void ConstructorWithoutThrow_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Money
            {
                public decimal Amount { get; }
                public Money(decimal amount) { Amount = amount; }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void ThrowInOrdinaryMethod_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            namespace Sample;
            public class Money
            {
                public void Fail() { throw new InvalidOperationException(); }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void FactoryReturningOneOfInsteadOfThrowing_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using OneOf;
            namespace Sample;
            public record InvalidAmount;
            public record Money
            {
                public decimal Amount { get; }
                private Money(decimal amount) { Amount = amount; }
                public static OneOf<Money, InvalidAmount> Create(decimal amount) =>
                    amount < 0 ? new InvalidAmount() : new Money(amount);
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }
}
