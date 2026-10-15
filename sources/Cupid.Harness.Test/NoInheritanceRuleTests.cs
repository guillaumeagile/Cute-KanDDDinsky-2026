using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class NoInheritanceRuleTests
{
    private readonly NoInheritanceRule _rule = new();

    [Fact]
    public void ClassInheritingFromDomainClass_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Base { }
            public class Derived : Base { }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Derived") && v.Message.Contains("Base"));
    }

    [Fact]
    public void RecordInheritingFromRecord_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public record Base;
            public record Derived : Base;
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Derived") && v.Message.Contains("Base"));
    }

    [Fact]
    public void ClassImplementingInterface_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public interface IWidget { }
            public class Widget : IWidget { }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void ClassInheritingFromException_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            namespace Sample;
            public class WidgetNotFoundException : Exception { }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }
}
