using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class NoPersistenceInDomainRuleTests
{
    private readonly NoPersistenceInDomainRule _rule = new();

    [Fact]
    public void EfCoreAttributeOnDomainProperty_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System.ComponentModel.DataAnnotations.Schema;
            namespace Sample;
            public class Product
            {
                [Column("ProductId")]
                public string Id { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("[Column]"));
    }

    [Fact]
    public void CsvShadowProperty_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public string DiscountsCsv { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("DiscountsCsv") && v.Message.Contains("shadow property"));
    }

    [Fact]
    public void JsonShadowProperty_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public string ImagesJson { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("ImagesJson") && v.Message.Contains("shadow property"));
    }

    [Fact]
    public void SyncEfColumnsMethod_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public void SyncEfColumns() { }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("SyncEfColumns"));
    }

    [Fact]
    public void HydrateFromEfColumnsMethod_ShouldBeFlagged()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public void HydrateFromEfColumns() { }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().ContainSingle(v => v.Message.Contains("HydrateFromEfColumns"));
    }

    [Fact]
    public void PlainDomainClass_ShouldPass()
    {
        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public string Id { get; set; }
                public string Name { get; set; }
                public void Deprecate() { }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void DbContextSubclass_ShouldBeExemptFromEfAttributeCheck()
    {
        // A DbContext is infrastructure - it's expected to know about the database schema.
        // We can't reference the real EF Core DbContext type in this in-memory compilation, so
        // this test only pins the exemption mechanism (matched by base type name "DbContext").
        var (trees, compilation) = RuleTestHarness.Compile("""
            using System.ComponentModel.DataAnnotations.Schema;
            namespace Sample;
            public class DbContext { }
            public class MyDbContext : DbContext
            {
                [Column("Foo")]
                public string Foo { get; set; }
            }
            """);

        var violations = _rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }
}
