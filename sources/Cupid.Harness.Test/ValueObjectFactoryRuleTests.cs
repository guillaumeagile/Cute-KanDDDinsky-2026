using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class ValueObjectFactoryRuleTests
{
    private readonly ValueObjectFactoryRule _rule = new();

    private const string Preamble = """
        using System;
        using OneOf;
        namespace Sample;
        public interface IValidationError { string Message { get; } }
        public record InvalidDates(string Message) : IValidationError;
        public record NotAValidationError(string Message);
        """;

    [Fact]
    public void CreateReturningOneOfSelfAndValidationError_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Perishable
            {
                public DateOnly SellByDate { get; }
                private Perishable(DateOnly sellBy) { SellByDate = sellBy; }
                public static OneOf<Perishable, InvalidDates> Create(DateOnly sellBy) => new Perishable(sellBy);
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void BuildReturningOneOfSelfAndValidationError_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Money(decimal Amount)
            {
                public static OneOf<Money, InvalidDates> Build(decimal amount) => new Money(amount);
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void OnlyOneValueObjectNeedsAFactory_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Plain(int X);
            public record Money(decimal Amount)
            {
                public static OneOf<Money, InvalidDates> Create(decimal amount) => new Money(amount);
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void NoFactoryAtAll_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Money(decimal Amount);
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Create/Build"));
    }

    [Fact]
    public void CreateReturningTheValueObjectDirectly_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Money(decimal Amount)
            {
                public static Money Create(decimal amount) => new Money(amount);
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle();
    }

    [Fact]
    public void ErrorNotImplementingValidationError_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Money(decimal Amount)
            {
                public static OneOf<Money, NotAValidationError> Create(decimal amount) => new Money(amount);
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle();
    }

    [Fact]
    public void OneOfOfAnotherTypeThanSelf_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Other(int X);
            public record Money(decimal Amount)
            {
                public static OneOf<Other, InvalidDates> Create(decimal amount) => new Other(1);
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle();
    }

    [Fact]
    public void FactoryNotNamedCreateOrBuild_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Money(decimal Amount)
            {
                public static OneOf<Money, InvalidDates> Make(decimal amount) => new Money(amount);
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle();
    }

    [Fact]
    public void FactoryOnAnIdentifiableRecord_IsNotAValueObject_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public interface IDentifiable { }
            public record Customer(string Name) : IDentifiable
            {
                public static OneOf<Customer, InvalidDates> Create(string name) => new Customer(name);
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle();
    }
}
