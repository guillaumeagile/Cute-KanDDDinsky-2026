using AwesomeAssertions;
using Cupid.Harness.Rules;

namespace Cupid.Harness.Test;

public class RichIdentifierRuleTests
{
    private readonly RichIdentifierRule _rule = new();

    private const string Preamble = """
        using System;
        namespace Sample;
        public interface IDentifiable { }
        public readonly struct Ulid { public static Ulid NewUlid() => default; }
        public record ProductId(Guid Value);
        public record SupplierId(Ulid Value);
        public record NameId(string Value);

        """;

    private IReadOnlyList<Violation> Run(string source)
    {
        var (trees, compilation) = RuleTestHarness.Compile(Preamble + source);
        return _rule.Check(trees, compilation);
    }

    [Fact]
    public void WrapperRecordOverGuid_ShouldPass() =>
        Run("public class Product : IDentifiable { public ProductId Id { get; } }").Should().BeEmpty();

    [Fact]
    public void WrapperRecordOverUlid_ShouldPass() =>
        Run("public class Supplier : IDentifiable { public SupplierId Id { get; } }").Should().BeEmpty();

    [Fact]
    public void BareGuidId_ShouldPassThisRule() =>
        Run("public class Product : IDentifiable { public Guid Id { get; } }").Should().BeEmpty();

    [Fact]
    public void BareUlidId_ShouldPassThisRule() =>
        Run("public class Product : IDentifiable { public Ulid Id { get; } }").Should().BeEmpty();

    [Fact]
    public void StringId_ShouldBeFlagged() =>
        Run("public class Product : IDentifiable { public string Id { get; set; } }")
            .Should().ContainSingle(v => v.Message.Contains("Id") && v.Message.Contains("string"));

    [Fact]
    public void IntAndLongIds_ShouldBeFlagged() =>
        Run("public class A : IDentifiable { public int Id { get; } } public class B : IDentifiable { public long Id { get; } }")
            .Should().HaveCount(2);

    [Fact]
    public void SuffixIdProperty_ShouldBeFlagged() =>
        Run("public class Stock { public string ProductId { get; } }")
            .Should().ContainSingle(v => v.Message.Contains("ProductId"));

    [Fact]
    public void ConstructorAndMethodParametersEndingWithId_ShouldBeFlagged() =>
        Run("public class Stock { public Stock(string supplierId) { } public void Move(int fromId) { } }")
            .Should().HaveCount(2);

    [Fact]
    public void RecordPrimaryConstructorParameter_ShouldBeFlaggedOnce() =>
        Run("public record Line(string ProductId, int Quantity);")
            .Should().ContainSingle(v => v.Message.Contains("ProductId"));

    [Fact]
    public void WrapperRecordOverString_ShouldBeFlagged() =>
        Run("public class Product : IDentifiable { public NameId Id { get; } }")
            .Should().ContainSingle(v => v.Message.Contains("NameId"));

    [Fact]
    public void ServicesNamespace_IsOutOfScope() =>
        Run("namespace Sample.Services { public class Svc { public string Id { get; } public void Do(string productId) { } } }")
            .Should().BeEmpty();

    [Fact]
    public void UnrelatedMembers_ShouldNotBeFlagged() =>
        Run("public class Thing { public string Name { get; } public string Valid { get; } public int Paid { get; } public string Grid { get; } }")
            .Should().BeEmpty();

    [Fact]
    public void GuidNewGuid_InIdentifiableType_ShouldBeFlagged() =>
        Run("public class Product : IDentifiable { public ProductId Id { get; } = new ProductId(Guid.NewGuid()); }")
            .Should().ContainSingle(v => v.Message.Contains("NewGuid") && v.Message.Contains("v7"));

    [Fact]
    public void GuidEmptyNewGuidAndDefault_InIdContext_ShouldBeFlagged() =>
        Run("""
            public class Product : IDentifiable
            {
                public ProductId A { get; } = new ProductId(Guid.Empty);
                public ProductId Id { get; } = new ProductId(new Guid());
                public ProductId OtherId { get; } = new ProductId(default(Guid));
            }
            """).Should().HaveCount(3);

    [Fact]
    public void GuidCreateVersion7AndUlidNewUlid_ShouldPass() =>
        Run("""
            public class Product : IDentifiable
            {
                public ProductId Id { get; } = new ProductId(Guid.CreateVersion7());
                public SupplierId SupplierId { get; } = new SupplierId(Ulid.NewUlid());
            }
            """).Should().BeEmpty();

    [Fact]
    public void NewGuid_AssignedToIdTarget_ShouldBeFlagged() =>
        Run("public class Factory { public void Make() { var productId = Guid.NewGuid(); } }")
            .Should().ContainSingle(v => v.Message.Contains("NewGuid"));

    [Fact]
    public void NewGuid_ForNonIdPurpose_ShouldNotBeFlagged() =>
        Run("public class Factory { public void Make() { var token = Guid.NewGuid(); } }").Should().BeEmpty();

    [Fact]
    public void FieldReturnTypeAndLocal_ShouldBeFlagged() =>
        Run("public class Stock { private string _productId = \"\"; public int GetSupplierId() => 0; public void Do() { string orderId = \"\"; } }")
            .Should().HaveCount(3);

    [Fact]
    public void ListOfStringIds_ShouldBeFlagged() =>
        Run("public class Stock { public System.Collections.Generic.List<string> ProductIds { get; } }")
            .Should().ContainSingle(v => v.Message.Contains("string"));

    [Fact]
    public void ListOfWrappedIds_ShouldPass() =>
        Run("public class Stock { public System.Collections.Generic.List<ProductId> ProductIds { get; } }").Should().BeEmpty();

    [Fact]
    public void VoidMethodNamedLikeAnId_ShouldPass() =>
        Run("public class Stock { public void ProcessId() { } }").Should().BeEmpty();
}
