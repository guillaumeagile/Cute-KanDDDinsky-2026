using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class AlgebraicDataTypeRuleTests
{
    private readonly AlgebraicDataTypeRule _rule = new();

    [Fact]
    public void NonAbstractUnionBase_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public record Shape;
            public sealed record Circle : Shape;
            public sealed record Square : Shape;
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("Shape") && v.Message.Contains("not 'abstract'"));
    }

    [Fact]
    public void NonSealedUnionCase_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public abstract record Shape;
            public record Circle : Shape;
            public sealed record Square : Shape;
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Circle") && v.Message.Contains("not 'sealed'"));
    }

    [Fact]
    public void WellFormedUnion_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public abstract record Shape;
            public sealed record Circle : Shape;
            public sealed record Square : Shape;
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    // Regression: a codebase with zero record hierarchies has nothing to check, so the rule
    // reports no violations - a vacuous PASS. That is wrong when the exercise (CUPID step 1.2:
    // Price rules that "may apply" Margin, then TransportationFee, then VAT) requires an ADT to
    // exist in the first place. Absence of any union is itself a HA3 failure.
    [Fact]
    public void NoRecordHierarchyAtAll_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Price
            {
                public decimal Amount { get; set; }
                public string Currency { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("No ADT"));
    }
}
