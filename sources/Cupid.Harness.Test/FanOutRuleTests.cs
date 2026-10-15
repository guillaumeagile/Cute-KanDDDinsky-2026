using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class FanOutRuleTests
{
    private readonly FanOutRule _rule = new();

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
    public void EntityTouchingFourEntityTypes_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }
            public class Warehouse : IDentifiable { public string Id { get; set; } }
            public class Price : IDentifiable { public string Id { get; set; } }
            public class Notification : IDentifiable { public string Id { get; set; } }

            public class Product : IDentifiable
            {
                public string Id { get; set; }

                private readonly Supplier _supplier;
                private readonly Warehouse _warehouse;
                private readonly Price _price;
                private readonly Notification _notification;

                public Product(Supplier supplier, Warehouse warehouse, Price price, Notification notification)
                {
                    _supplier = supplier;
                    _warehouse = warehouse;
                    _price = price;
                    _notification = notification;
                }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().HaveCountGreaterThanOrEqualTo(4); // one per reference: fields and ctor parameters
        violations.Should().OnlyContain(v => v.Message.Contains("'Product'") && v.Message.Contains("[4 distinct entity type(s)"));
        violations.Select(v => v.Message).Should().Contain(m => m.Contains("'Supplier'")).And.Contain(m => m.Contains("'Warehouse'")).And.Contain(m => m.Contains("'Price'")).And.Contain(m => m.Contains("'Notification'"));
    }

    [Fact]
    public void EntityTouchingThreeEntityTypes_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }
            public class Warehouse : IDentifiable { public string Id { get; set; } }
            public class Price : IDentifiable { public string Id { get; set; } }

            public class Product : IDentifiable
            {
                public string Id { get; set; }
                public Supplier Supplier { get; set; }
                public Warehouse Warehouse { get; set; }
                public Price Price { get; set; }
            }
            """);

        _rule.Check(trees, compilation).Should().HaveCount(3).And.OnlyContain(v => v.Message.Contains("[3 distinct entity type(s)"));
    }

    [Fact]
    public void EntityReferencingASingleOtherEntity_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Warehouse : IDentifiable { public string Id { get; set; } }

            public class Product : IDentifiable
            {
                public string Id { get; set; }
                public Warehouse IsStoredIn { get; set; }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Product") && v.Message.Contains("Warehouse"));
    }

    [Fact]
    public void EntityReferencingEntityInsideGenericsOrArrays_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            using System.Collections.Generic;
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }
            public class Warehouse : IDentifiable { public string Id { get; set; } }

            public class Product : IDentifiable
            {
                public string Id { get; set; }
                public Dictionary<string, Supplier> Suppliers { get; set; }
                public Warehouse[] Warehouses { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);
        violations.Should().ContainSingle(v => v.Message.Contains("'Supplier'") && v.Message.Contains("property 'Suppliers'"));
        violations.Should().ContainSingle(v => v.Message.Contains("'Warehouse'") && v.Message.Contains("property 'Warehouses'"));
    }

    [Fact]
    public void EntityReferencingMethodParameterAndReturnType_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }

            public class Product : IDentifiable
            {
                public string Id { get; set; }
                public Supplier Pick(Supplier s) => s;
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("Supplier"));
    }

    [Fact]
    public void EntityReferencingOtherEntitiesByIdOnly_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            using System;
            using System.Collections.Generic;
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }
            public class Warehouse : IDentifiable { public string Id { get; set; } }

            public class Product : IDentifiable
            {
                public string Id { get; set; }
                public Guid? WarehouseId { get; set; }
                public Dictionary<string, Guid> SupplierIdsByRegion { get; set; }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void SameNamedTypeInAnotherNamespace_ShouldNotBeConfusedWithEntity()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace Other { public class Supplier { } }
            namespace OmniProduct_CoreDomain.Models
            {
                using OmniProduct_CoreDomain.Abstractions;
                public class Supplier : IDentifiable { public string Id { get; set; } }
                public class Product : IDentifiable
                {
                    public string Id { get; set; }
                    public Other.Supplier External { get; set; }
                }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void NonEntityClassTouchingFourEntityTypes_ShouldNotBeFlagged()
    {
        // HA7 only applies to entities. A service wiring together many entities is expected -
        // that's what services do - and is out of scope for this rule entirely.
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models
            {
                using OmniProduct_CoreDomain.Abstractions;
                public class Supplier : IDentifiable { public string Id { get; set; } }
                public class Warehouse : IDentifiable { public string Id { get; set; } }
                public class Price : IDentifiable { public string Id { get; set; } }
                public class Notification : IDentifiable { public string Id { get; set; } }
            }
            namespace OmniProduct_CoreDomain.Services
            {
            using OmniProduct_CoreDomain.Models;
            public class ProductService
            {
                private readonly Supplier _supplier;
                private readonly Warehouse _warehouse;
                private readonly Price _price;
                private readonly Notification _notification;

                public ProductService(Supplier supplier, Warehouse warehouse, Price price, Notification notification)
                {
                    _supplier = supplier;
                    _warehouse = warehouse;
                    _price = price;
                    _notification = notification;
                }
            }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void RecordValueObjects_ShouldNotCountTowardFanOut()
    {
        // Records are value objects, not entities, so an entity may reference any number of them.
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public record Money(decimal Amount);
            public record Slug(string Value);
            public record TaxRate(decimal Value);

            public class Product : IDentifiable
            {
                public string Id { get; set; }
                public Money Money { get; set; }
                public Slug Slug { get; set; }
                public TaxRate TaxRate { get; set; }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void ClassValueObjectWithoutIDentifiable_ShouldBeFreelyReferenceable()
    {
        // Price-style value object: a plain class without IDentifiable may be referenced by any entity.
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Price { public decimal Amount { get; set; } }
            public class Product : IDentifiable
            {
                public string Id { get; set; }
                public Price Price { get; set; }
                public Price Discounted(Price p) => p;
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void SubjectNotImplementingIDentifiable_ShouldStillBeChecked()
    {
        // Regression: HA7 used to skip any type lacking IDentifiable, so a Product that omitted the
        // interface (as the real domain did) passed vacuously. Every Models class is a checked subject.
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Warehouse : IDentifiable { public string Id { get; set; } }
            public class Product
            {
                public string Id { get; set; }
                public Warehouse IsStoredIn { get; set; }
            }
            """);

        _rule.Check(trees, compilation).Should().ContainSingle(v => v.Message.Contains("'Product'") && v.Message.Contains("'Warehouse'"));
    }

    [Fact]
    public void Diagnostic_ShouldNameEntityMemberAndLine_PerReference()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }
            public class Warehouse : IDentifiable { public string Id { get; set; } }
            public class Product
            {
                public Warehouse IsStoredIn { get; set; }
                public void Pick(Supplier s) { }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().HaveCount(2);
        var first = violations.Min(v => v.Line);
        violations.Should().ContainSingle(v => v.Line == first && v.Message.Contains("'Warehouse'") && v.Message.Contains("property 'IsStoredIn'") && v.Message.Contains("WarehouseId"));
        violations.Should().ContainSingle(v => v.Line == first + 1 && v.Message.Contains("'Supplier'") && v.Message.Contains("method 'Pick'") && v.Message.Contains("SupplierId"));
    }

    [Fact]
    public void LocalVariableOfEntityType_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }
            public class Product
            {
                public void Run() { Supplier s = null; }
            }
            """);

        _rule.Check(trees, compilation).Should().Contain(v => v.Message.Contains("'Supplier'") && v.Message.Contains("method 'Run'"));
    }

    [Fact]
    public void EntitiesReferencingEachOtherBidirectionally_ShouldBothBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            using System.Collections.Generic;
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Product : IDentifiable { public string Id { get; set; } public Warehouse Warehouse { get; set; } }
            public class Warehouse : IDentifiable { public string Id { get; set; } public List<Product> Products { get; set; } }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("'Product' references entity 'Warehouse'"));
        violations.Should().Contain(v => v.Message.Contains("'Warehouse' references entity 'Product'"));
    }

    [Fact]
    public void DbContextSubclass_ShouldNotBeTreatedAsEntity()
    {
        // EF isn't referenced by the harness compilation; a locally declared DbContext stands in.
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System.Collections.Generic;
            namespace Microsoft.EntityFrameworkCore { public class DbContext { } public class DbSet<T> { } }
            namespace OmniProduct_CoreDomain.Models
            {
                using Microsoft.EntityFrameworkCore;
                public class Supplier { }
                public class Warehouse { }
                public class Ctx : DbContext
                {
                    public DbSet<Supplier> Suppliers { get; set; }
                    public DbSet<Warehouse> Warehouses { get; set; }
                }
            }
            """);

        _rule.Check(trees, compilation).Should().BeEmpty();
    }

    [Fact]
    public void IDentifiableTypesOutsideModelsNamespace_ShouldNotCountTowardFanOutOrBeSubjectToIt()
    {
        // HA9 should already forbid an IDentifiable implementer outside Models.*, but HA7 doesn't
        // rely on that - it only trusts entities declared under OmniProduct_CoreDomain.Models.*,
        // both as the type under test and as what counts toward its fan-out.
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace Sample;
            using OmniProduct_CoreDomain.Abstractions;
            public class Supplier : IDentifiable { public string Id { get; set; } }
            public class Warehouse : IDentifiable { public string Id { get; set; } }
            public class Price : IDentifiable { public string Id { get; set; } }
            public class Notification : IDentifiable { public string Id { get; set; } }

            public class Product : IDentifiable
            {
                public string Id { get; set; }

                private readonly Supplier _supplier;
                private readonly Warehouse _warehouse;
                private readonly Price _price;
                private readonly Notification _notification;

                public Product(Supplier supplier, Warehouse warehouse, Price price, Notification notification)
                {
                    _supplier = supplier;
                    _warehouse = warehouse;
                    _price = price;
                    _notification = notification;
                }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void SelfReferences_ShouldNotCountTowardOwnFanOut()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Node : IDentifiable
            {
                public string Id { get; set; }
                public Node? Next { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void BclTypes_ShouldNotCountTowardFanOut()
    {
        var (trees, compilation) = RuleTestHarness.Compile(IDentifiable + """
            using System;
            using System.Collections.Generic;
            namespace OmniProduct_CoreDomain.Models;
            using OmniProduct_CoreDomain.Abstractions;
            public class Widget : IDentifiable
            {
                public string Id { get; set; }

                private readonly List<string> _tags = new();
                private readonly Guid _guid = Guid.NewGuid();
                private readonly DateTime _createdAt = DateTime.Now;
                private readonly Dictionary<string, string> _images = new();
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }
}
