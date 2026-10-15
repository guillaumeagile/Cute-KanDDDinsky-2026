using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class NoServicesInEntityConstructorRuleTests
{
    private readonly NoServicesInEntityConstructorRule _rule = new();

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
    public void EntityConstructor_TakingServiceByNamespace_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Services
            {
                public class NotificationDispatcher { }
            }

            namespace OmniProduct_CoreDomain.Models;
            public class Product : OmniProduct_CoreDomain.Abstractions.IDentifiable
            {
                public string Id { get; set; }

                public Product(string id, OmniProduct_CoreDomain.Services.NotificationDispatcher dispatcher)
                {
                    Id = id;
                }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Product") && v.Message.Contains("NotificationDispatcher"));
    }

    [Fact]
    public void EntityConstructor_TakingTypeNamedService_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            public class PricingService { }

            public class Product : OmniProduct_CoreDomain.Abstractions.IDentifiable
            {
                public string Id { get; set; }

                public Product(string id, PricingService pricingService)
                {
                    Id = id;
                }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Product") && v.Message.Contains("PricingService"));
    }

    [Fact]
    public void EntityConstructor_TakingOnlyPlainValuesAndEntities_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            public class Supplier : OmniProduct_CoreDomain.Abstractions.IDentifiable
            {
                public string Id { get; set; }
            }

            public class Product : OmniProduct_CoreDomain.Abstractions.IDentifiable
            {
                public string Id { get; set; }

                public Product(string id, string name, Supplier supplier)
                {
                    Id = id;
                }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void RecordEntity_WithPrimaryConstructorTakingService_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Services
            {
                public class PricingService { }
            }

            namespace OmniProduct_CoreDomain.Models;
            public record Price(string Id, OmniProduct_CoreDomain.Services.PricingService PricingService) : OmniProduct_CoreDomain.Abstractions.IDentifiable;
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("Price") && v.Message.Contains("PricingService"));
    }

    [Fact]
    public void NonEntityType_TakingService_ShouldNotBeFlagged()
    {
        // A service consuming another service is normal - HA10 only restricts entities.
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Services
            {
                public class SupplierService { }

                public class CatalogService
                {
                    public CatalogService(SupplierService supplierService) { }
                }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }
}
