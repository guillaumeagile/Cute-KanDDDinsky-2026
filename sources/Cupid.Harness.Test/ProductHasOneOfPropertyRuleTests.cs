using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class ProductHasOneOfPropertyRuleTests
{
    private readonly ProductHasOneOfPropertyRule _rule = new();

    private const string Preamble = """
        using System;
        using OneOf;
        namespace Sample;
        public interface IDentifiable { }
        public record Perishable(DateOnly SellByDate, DateOnly UseByDate);
        public record NonExpiring(DateOnly BestBeforeDate);
        """;

    [Fact]
    public void GetOnlyOneOfPropertyOfValueObjects_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Product { public OneOf<Perishable, NonExpiring> ShelfLife { get; } }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void InitOnlyOneOfProperty_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Product { public OneOf<Perishable, NonExpiring> ShelfLife { get; init; } }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void SeveralOneOfProperties_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Active;
            public record Retired;
            public class Product
            {
                public OneOf<Perishable, NonExpiring> ShelfLife { get; }
                public OneOf<Active, Retired> Lifecycle { get; }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void NoProductType_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Item { public OneOf<Perishable, NonExpiring> ShelfLife { get; } }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("No 'Product' type"));
    }

    [Fact]
    public void ProductWithoutOneOfProperty_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Product { public string Name { get; } }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Product") && v.Message.Contains("OneOf"));
    }

    [Fact]
    public void OneOfPropertyWithSetter_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Product { public OneOf<Perishable, NonExpiring> ShelfLife { get; set; } }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("ShelfLife") && v.Message.Contains("set"));
    }

    [Fact]
    public void TypeArgumentThatIsAClass_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class Frozen { }
            public class Product { public OneOf<Perishable, Frozen> ShelfLife { get; } }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Frozen") && v.Message.Contains("value object"));
    }

    [Fact]
    public void TypeArgumentThatIsAnIdentifiableRecord_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public record Customer : IDentifiable;
            public class Product { public OneOf<Perishable, Customer> ShelfLife { get; } }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Customer") && v.Message.Contains("value object"));
    }

    [Fact]
    public void PropertyTypedAsOneOfBaseSubclass_DoesNotCountAsOneOf()
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + """
            public class ShelfLife : OneOfBase<Perishable, NonExpiring>
            {
                public ShelfLife(OneOf<Perishable, NonExpiring> input) : base(input) { }
            }
            public class Product { public ShelfLife ShelfLife { get; } }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Product") && v.Message.Contains("OneOf"));
    }
}
