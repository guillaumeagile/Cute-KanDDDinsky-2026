using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class NoClockInModelRuleTests
{
    private readonly NoClockInModelRule _rule = new();

    private const string Preamble = """
        using System;
        namespace Sample;
        public interface IDentifiable { }
        """;

    [Fact]
    public void DateTimeNowInEntity_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Product : IDentifiable
            {
                public bool CanSell(DateTime sellBy) => DateTime.Now <= sellBy;
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Product") && v.Message.Contains("DateTime.Now"));
    }

    [Fact]
    public void DateTimeUtcNowInValueObject_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Perishable(DateOnly SellByDate)
            {
                public bool IsPast() => DateOnly.FromDateTime(DateTime.UtcNow) > SellByDate;
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Perishable") && v.Message.Contains("DateTime.UtcNow"));
    }

    [Fact]
    public void DateTimeToday_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Perishable(DateTime SellByDate) { public bool IsPast() => DateTime.Today > SellByDate; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("DateTime.Today"));
    }

    [Fact]
    public void DateTimeOffsetUtcNow_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Perishable(DateTimeOffset SellBy) { public bool IsPast() => DateTimeOffset.UtcNow > SellBy; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("DateTimeOffset.UtcNow"));
    }

    [Fact]
    public void TimeProviderSystemInModel_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Perishable(DateTimeOffset SellBy) { public bool IsPast() => TimeProvider.System.GetUtcNow() > SellBy; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("TimeProvider.System"));
    }

    [Fact]
    public void UsingStaticDateTimeNow_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System;
            using static System.DateTime;
            namespace Sample;
            public record Perishable(DateTime SellBy) { public bool IsPast() => Now > SellBy; }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("DateTime.Now"));
    }

    [Fact]
    public void TodayPassedAsParameter_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Perishable(DateOnly SellByDate) { public bool IsPast(DateOnly today) => today > SellByDate; }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void InjectedTimeProviderInstance_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Perishable(DateTimeOffset SellBy) { public bool IsPast(TimeProvider clock) => clock.GetUtcNow() > SellBy; }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void ClockReadInAService_IsOutOfScope_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class ClockService { public DateTime Now() => DateTime.Now; }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }
}
