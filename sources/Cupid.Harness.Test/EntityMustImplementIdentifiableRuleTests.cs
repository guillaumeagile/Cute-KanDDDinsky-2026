using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class EntityMustImplementIdentifiableRuleTests
{
    private readonly EntityMustImplementIdentifiableRule _rule = new();

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
    public void ClassInModelsNamespace_WithoutIDentifiable_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            public class Product
            {
                public string Name { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Product") && v.Message.Contains("IDentifiable"));
    }

    [Fact]
    public void ClassInModelsNamespace_ImplementingIDentifiable_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            public class Product : OmniProduct_CoreDomain.Abstractions.IDentifiable
            {
                public string Id { get; set; }
                public string Name { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void RecordInModelsNamespace_WithoutIDentifiable_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            public record Price(decimal Amount);
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Price"));
    }

    [Fact]
    public void NestedNamespaceUnderModels_WithoutIDentifiable_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models.Storage;
            public class Warehouse
            {
                public string Name { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Warehouse"));
    }

    [Fact]
    public void ClassOutsideModelsNamespace_WithoutIDentifiable_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Services;
            public class PricingCalculator
            {
                public decimal Calculate() => 0m;
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }
}
