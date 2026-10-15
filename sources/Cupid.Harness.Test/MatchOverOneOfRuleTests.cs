using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class MatchOverOneOfRuleTests
{
    private readonly MatchOverOneOfRule _rule = new();

    private const string Preamble = """
        using System;
        using OneOf;
        namespace Sample;
        public record Perishable(DateOnly SellByDate);
        public record NonExpiring(DateOnly BestBeforeDate);
        public class Label
        {
            public static string Of(OneOf<Perishable, NonExpiring> shelf) => shelf.Match(p => "perishable", n => "non-expiring");
        }
        """;

    [Fact]
    public void MatchOverOneOfOfRecords_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void IsPatternOnAnAlternative_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Bad { public bool Check(OneOf<Perishable, NonExpiring> s) => s.Value is Perishable; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'is'"));
    }

    [Fact]
    public void IsDeclarationPatternOnAnAlternative_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Bad { public bool Check(OneOf<Perishable, NonExpiring> s) => s.Value is Perishable p && p.SellByDate.Year > 0; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'is'"));
    }

    [Fact]
    public void AsOnAnAlternative_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Bad { public Perishable? Check(OneOf<Perishable, NonExpiring> s) => s.Value as Perishable; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'as'"));
    }

    [Fact]
    public void SwitchStatementOnAlternatives_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Bad
            {
                public int Check(OneOf<Perishable, NonExpiring> s)
                {
                    switch (s.Value)
                    {
                        case Perishable p: return 1;
                        default: return 0;
                    }
                }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'switch'"));
    }

    [Fact]
    public void SwitchExpressionOnAlternatives_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Bad { public int Check(OneOf<Perishable, NonExpiring> s) => s.Value switch { Perishable => 1, _ => 0 }; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'switch'"));
    }

    [Fact]
    public void SwitchDirectlyOnTheOneOf_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Bad { public int Check(OneOf<Perishable, NonExpiring> s) => s switch { _ => 0 }; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'switch'"));
    }

    [Fact]
    public void TypeTestsOnUnrelatedTypes_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Fine
            {
                public bool Check(object o) => o is string || o is null;
                public int Number(object o) => o switch { int i => i, _ => 0 };
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void NoMatchCallAtAll_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            using OneOf;
            namespace Sample;
            public record Perishable(DateOnly SellByDate);
            public record NonExpiring(DateOnly BestBeforeDate);
            public class Holder { public OneOf<Perishable, NonExpiring> Shelf { get; } }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("No .Match"));
    }

    [Fact]
    public void MatchOverAOneOfWithAClassArgument_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            using OneOf;
            namespace Sample;
            public record Perishable(DateOnly SellByDate);
            public class Frozen { }
            public class Label
            {
                public static string Of(OneOf<Perishable, Frozen> shelf) => shelf.Match(p => "a", f => "b");
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("No .Match"));
    }
}
