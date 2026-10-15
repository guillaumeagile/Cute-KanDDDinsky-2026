using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class BareIdentifierIsWeakRuleTests
{
    private readonly BareIdentifierIsWeakRule _rule = new();

    private const string Preamble = """
        using System;
        namespace Sample;
        public readonly struct Ulid { }
        public record ProductId(Guid Value);

        """;

    private IReadOnlyList<Violation> Run(string source)
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + source);
        return _rule.Check(trees, compilation);
    }

    [Fact]
    public void IsAWarning() => _rule.Severity.Should().Be(HarnessSeverity.Warning);

    [Fact]
    public void BareGuidId_ShouldWarn() =>
        Run("public class Product { public Guid Id { get; } }").Should().ContainSingle(v => v.Message.Contains("Guid"));

    [Fact]
    public void BareUlidSuffixId_ShouldWarn() =>
        Run("public class Stock { public Ulid SupplierId { get; } }").Should().ContainSingle(v => v.Message.Contains("Ulid"));

    [Fact]
    public void WrapperRecord_ShouldNotWarn() =>
        Run("public class Product { public ProductId Id { get; } }").Should().BeEmpty();

    [Fact]
    public void StringId_ShouldWarn() =>
        Run("public class Product { public string Id { get; } }").Should().ContainSingle(v => v.Message.Contains("string"));

    [Fact]
    public void IntSuffixId_ShouldWarn() =>
        Run("public class Stock { public int ProductId { get; } }").Should().ContainSingle(v => v.Message.Contains("int"));

    [Fact]
    public void SourceDeclaredWrapperOverString_IsLeftToHA12() =>
        Run("public record NameId(string Value); public class Product { public NameId Id { get; } }").Should().BeEmpty();

    [Fact]
    public void ServicesNamespace_IsInScope() =>
        Run("namespace Sample.Services { public class Svc { public void Do(string productId) { } } }")
            .Should().ContainSingle(v => v.Message.Contains("productId"));

    [Fact]
    public void Field_ShouldWarn() =>
        Run("public class Stock { private readonly string _productId = \"\"; }").Should().ContainSingle(v => v.Message.Contains("_productId"));

    [Fact]
    public void Local_ShouldWarn() =>
        Run("public class Stock { public void Do() { string productId = \"\"; } }").Should().ContainSingle(v => v.Message.Contains("productId"));

    [Fact]
    public void ReturnType_ShouldWarn() =>
        Run("public class Stock { public string GetProductId() => \"\"; }").Should().ContainSingle(v => v.Message.Contains("GetProductId"));

    [Fact]
    public void VoidMethodNamedLikeAnId_ShouldNotWarn() =>
        Run("public class Stock { public void ProcessId() { } }").Should().BeEmpty();

    [Fact]
    public void ListOfStringIds_ShouldWarn() =>
        Run("public class Stock { public System.Collections.Generic.List<string> ProductIds { get; } }")
            .Should().ContainSingle(v => v.Message.Contains("string"));

    [Fact]
    public void ArrayOfGuidIds_ShouldWarn() =>
        Run("public class Stock { public Guid[] ProductIds { get; } }").Should().ContainSingle(v => v.Message.Contains("Guid"));

    [Fact]
    public void ListOfWrappedIds_ShouldNotWarn() =>
        Run("public class Stock { public System.Collections.Generic.List<ProductId> ProductIds { get; } }").Should().BeEmpty();

    [Fact]
    public void DictionaryKeyedByStringId_WarnsOnTheKeyOnly() =>
        Run("public class Product { } public class Stock { public System.Collections.Generic.Dictionary<string, Product> SupplierIds { get; } }")
            .Should().ContainSingle(v => v.Message.Contains("string"));

    [Fact]
    public void NullableGuidId_ShouldWarn() =>
        Run("public class Product { public Guid? Id { get; } }").Should().ContainSingle(v => v.Message.Contains("Guid"));

    [Fact]
    public void UriId_ShouldWarn() =>
        Run("public class Product { public Uri Id { get; } }").Should().ContainSingle(v => v.Message.Contains("Uri"));
}
