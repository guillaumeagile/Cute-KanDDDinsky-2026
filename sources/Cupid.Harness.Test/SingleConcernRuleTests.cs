using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class SingleConcernRuleTests
{
    private static UbiquitousLanguageMap SampleMap()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            {
              "concerns": {
                "Catalog": ["Image", "Catalog"],
                "Pricing": ["ResellerPrice", "Margin"],
                "Storage": ["Stock"]
              }
            }
            """);
        try
        {
            return UbiquitousLanguageMap.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static UbiquitousLanguageMap SampleMapWithRestrictions()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            {
              "concerns": {
                "Catalog": ["Image", "Catalog"],
                "Pricing": ["ResellerPrice", "Margin"],
                "Storage": ["Stock"]
              },
              "restrictions": {
                "Product": ["Image", "Margin", "Slug"]
              }
            }
            """);
        try
        {
            return UbiquitousLanguageMap.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ClassMixingTwoConcerns_ShouldBeFlagged()
    {
        var rule = new SingleConcernRule(SampleMap(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class ProductService
            {
                public void AddImage(string url) { }
                public void SetMargin(decimal margin) { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().ContainSingle(v =>
            v.Message.Contains("ProductService") &&
            v.Message.Contains("Catalog") &&
            v.Message.Contains("Pricing"));
    }

    [Fact]
    public void ClassWithSingleConcern_ShouldPass()
    {
        var rule = new SingleConcernRule(SampleMap(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class CatalogService
            {
                public void ImagePost(string url) { }
                public void ReadCatalog() { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void ClassWithNoRecognizedConcern_ShouldPass()
    {
        var rule = new SingleConcernRule(SampleMap(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Utility
            {
                public void Frobnicate() { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void ThreeConcernsMixed_ShouldReportAllThreeInMessage()
    {
        var rule = new SingleConcernRule(SampleMap(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class ProductService
            {
                public void AddImage(string url) { }
                public void SetMargin(decimal margin) { }
                public void ReceiveStock(int quantity) { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().ContainSingle();
        violations[0].Message.Should().Contain("Catalog");
        violations[0].Message.Should().Contain("Pricing");
        violations[0].Message.Should().Contain("Storage");
    }

    [Fact]
    public void RestrictedKeywordOnNamedType_ShouldBeFlagged()
    {
        var rule = new SingleConcernRule(SampleMapWithRestrictions(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public string Slug { get; set; }
                public void AddImage(string url) { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().ContainSingle(v =>
            v.Message.Contains("Product.AddImage") &&
            v.Message.Contains("restricted vocabulary 'Image'"));

        violations.Should().ContainSingle(v =>
            v.Message.Contains("Product.Slug") &&
            v.Message.Contains("restricted vocabulary 'Slug'"));
    }

    [Fact]
    public void RestrictedKeyword_OnlyAppliesToNamedType()
    {
        var rule = new SingleConcernRule(SampleMapWithRestrictions(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class ProductCatalog
            {
                public void AddImage(string url) { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void NonRestrictedKeywordOnNamedType_ShouldPass()
    {
        var rule = new SingleConcernRule(SampleMapWithRestrictions(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public void ReceiveStock(int quantity) { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void MultipleRestrictedKeywordsOnSameMethod_ShouldEachBeFlagged()
    {
        var rule = new SingleConcernRule(SampleMapWithRestrictions(), maxConcernsPerClass: 1);

        var (trees, compilation) = RuleTestHarness.Compile("""
            namespace Sample;
            public class Product
            {
                public void SetImageMargin(decimal margin) { }
            }
            """);

        var violations = rule.Check(trees, compilation);

        violations.Should().Contain(v => v.Message.Contains("restricted vocabulary 'Image'"));
        violations.Should().Contain(v => v.Message.Contains("restricted vocabulary 'Margin'"));
    }
}
