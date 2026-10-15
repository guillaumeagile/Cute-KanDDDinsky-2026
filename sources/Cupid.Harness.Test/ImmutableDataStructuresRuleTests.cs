using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class ImmutableDataStructuresRuleTests
{
    private readonly ImmutableDataStructuresRule _rule = new();

    [Fact]
    public void PlainClass_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Widget
            {
                public string Name { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("is a class, not a record"));
    }

    [Fact]
    public void ClassInServicesNamespace_ShouldNotBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample.Services;
            public class WidgetService
            {
                public void Run() { }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().NotContain(v => v.Message.Contains("is a class, not a record"));
    }

    [Fact]
    public void MutableCollectionInServicesNamespace_ShouldNotBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System.Collections.Generic;
            namespace Sample.Services;
            public class WidgetService
            {
                private readonly List<string> _names = new();
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().NotContain(v => v.Message.Contains("mutable collection type"));
    }

    [Fact]
    public void PropertyWithSetter_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public record Widget
            {
                public string Name { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("mutable 'set' accessor"));
    }

    [Fact]
    public void MutableCollectionProperty_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public record Widget
            {
                public List<string> Tags { get; init; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("mutable collection type"));
    }

    [Fact]
    public void RecordWithInitOnlyPropertiesAndImmutableCollection_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System.Collections.Immutable;
            namespace Sample;
            public record Widget
            {
                public string Name { get; init; }
                public ImmutableList<string> Tags { get; init; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }
}
