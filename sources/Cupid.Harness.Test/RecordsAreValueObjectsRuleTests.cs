using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class RecordsAreValueObjectsRuleTests
{
    private readonly RecordsAreValueObjectsRule _rule = new();

    private const string IDentifiable = """
        namespace OmniProduct_CoreDomain.Abstractions
        {
            public interface IDentifiable
            {
                string Id { get; set; }
            }
        }

        """;

    [Fact]
    public void RecordImplementingIDentifiable_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency) : OmniProduct_CoreDomain.Abstractions.IDentifiable
            {
                string OmniProduct_CoreDomain.Abstractions.IDentifiable.Id { get; set; } = "";
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("Price") && v.Message.Contains("IDentifiable"));
    }

    [Fact]
    public void PriceAsPlainRecordUnderValueObjects_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void PriceRecordUnderModels_ShouldBeFlaggedAsMissing()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.Models;
            public record Price(decimal Amount, string Currency);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Price") && v.Message.Contains("ValueObjects"));
    }

    [Fact]
    public void MissingPriceRecordUnderValueObjects_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Margin(decimal Percentage);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Price") && v.Message.Contains("ValueObjects"));
    }

    [Fact]
    public void ClassUnderValueObjects_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            public class Margin { public decimal Percentage { get; } }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Margin") && v.Message.Contains("class"));
    }

    [Fact]
    public void ClassOutsideValueObjects_ShouldNotBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects
            {
                public record Price(decimal Amount, string Currency);
            }
            namespace OmniProduct_CoreDomain.Models
            {
                public class Product { }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void ImmutablePositionalRecord_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            public record Perishable(System.DateOnly SellByDate, System.DateOnly UseByDate);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void ReadonlyRecordStruct_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            public readonly record struct NonExpiring(System.DateOnly BestBeforeDate);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void RecordWithInitOnlyProperty_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            public record Money { public decimal Amount { get; init; } }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void RecordWithSetter_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            public record Money { public decimal Amount { get; set; } }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Money") && v.Message.Contains("Amount"));
    }

    [Fact]
    public void RecordWithMutableCollection_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            public record Basket(System.Collections.Generic.List<string> Items);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Basket") && v.Message.Contains("Items"));
    }

    [Fact]
    public void RecordWithMutableField_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace OmniProduct_CoreDomain.ValueObjects;
            public record Price(decimal Amount, string Currency);
            public record Money { public decimal Amount; }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Money") && v.Message.Contains("Amount"));
    }
}
