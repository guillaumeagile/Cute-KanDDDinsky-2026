using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class NoGodClassRuleTests
{
    private readonly NoGodClassRule _rule = new(maxPublicProperties: 6);

    [Fact]
    public void ClassWithSevenPublicProperties_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Widget
            {
                public string P1 { get; set; }
                public string P2 { get; set; }
                public string P3 { get; set; }
                public string P4 { get; set; }
                public string P5 { get; set; }
                public string P6 { get; set; }
                public string P7 { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("7 public properties"));
    }

    [Fact]
    public void ClassWithSixPublicProperties_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Widget
            {
                public string P1 { get; set; }
                public string P2 { get; set; }
                public string P3 { get; set; }
                public string P4 { get; set; }
                public string P5 { get; set; }
                public string P6 { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void PrivateProperties_ShouldNotCount()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Widget
            {
                public string P1 { get; set; }
                private string P2 { get; set; }
                private string P3 { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void PositionalRecordParameters_ShouldCountAsPublicProperties()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public record Widget(string P1, string P2, string P3, string P4, string P5, string P6, string P7);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("7 public properties"));
    }

    [Fact]
    public void IdSuffixedProperties_ShouldNotCount()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Widget
            {
                public string Id { get; set; }
                public string ProductId { get; set; }
                public string WarehouseId { get; set; }
                public string P1 { get; set; }
                public string P2 { get; set; }
                public string P3 { get; set; }
                public string P4 { get; set; }
                public string P5 { get; set; }
                public string P6 { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void IdSuffixedPositionalRecordParameters_ShouldNotCount()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public record Widget(string Id, string ProductId, string P1, string P2, string P3, string P4, string P5, string P6);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }
}
