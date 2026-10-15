using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA4.3 - Composable, step 2: Product carries its differences as composed values, not subclasses.
// `Product` must have at least one property typed OneOf<T0, T1, ...> (the real OneOf.OneOf<> type, not an
// OneOfBase subclass), each with no setter (get-only or init), and every type argument must be a value
// object (see RecordsAreValueObjectsRule.IsValueObject). Property names are not pinned.
public sealed class ProductHasOneOfPropertyRule : IHarnessRule
{
    private const string ProductTypeName = "Product";

    public string Id => "HA4.3";
    public string Name => "Product has at least one OneOf<...> property: no setter, type arguments are value objects";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();
        var productCount = 0;

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (typeDecl.Identifier.Text != ProductTypeName
                    || model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol product)
                    continue;

                productCount++;
                violations.AddRange(CheckProduct(tree, typeDecl, product));
            }
        }

        if (productCount == 0)
        {
            violations.Add(new Violation(
                trees.FirstOrDefault()?.FilePath ?? string.Empty,
                1,
                $"No '{ProductTypeName}' type found. It must have at least one OneOf<...> property."));
        }

        return violations;
    }

    private static IEnumerable<Violation> CheckProduct(SyntaxTree tree, TypeDeclarationSyntax typeDecl, INamedTypeSymbol product)
    {
        var oneOfProperties = product.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && IsOneOf(p.Type))
            .ToList();

        if (oneOfProperties.Count == 0)
        {
            yield return At(tree, typeDecl.Identifier.GetLocation(),
                $"'{product.Name}' has no OneOf<...> property. Compose it with a OneOf<...> of value objects (not OneOfBase, not inheritance).");
            yield break;
        }

        foreach (var property in oneOfProperties)
        {
            var location = property.Locations.FirstOrDefault() ?? typeDecl.Identifier.GetLocation();

            if (property.SetMethod is { IsInitOnly: false })
                yield return At(tree, location, $"'{product.Name}.{property.Name}' has a mutable 'set'. Use 'init' or make it get-only.");

            foreach (var typeArgument in ((INamedTypeSymbol)property.Type).TypeArguments)
            {
                if (typeArgument is not INamedTypeSymbol named || !RecordsAreValueObjectsRule.IsValueObject(named))
                    yield return At(tree, location,
                        $"'{product.Name}.{property.Name}': type argument '{typeArgument.Name}' is not a value object (it must be a record that does not implement IDentifiable).");
            }
        }
    }

    private static bool IsOneOf(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsGenericType: true } named
        && named.OriginalDefinition.Name == "OneOf"
        && named.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "OneOf";

    private static Violation At(SyntaxTree tree, Location location, string message) =>
        new(tree.FilePath, location.GetLineSpan().StartLinePosition.Line + 1, message);
}
