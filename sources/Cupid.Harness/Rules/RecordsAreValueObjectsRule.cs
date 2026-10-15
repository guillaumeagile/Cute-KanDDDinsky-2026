using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA3 - Records are Value Objects, never entities. Four checks:
//  1. a record must not implement IDentifiable - identity belongs to entities (HA9), and a record's job
//     is to be a Value Object (equality by value, no identity);
//  2. a value object is immutable: no setter, no mutable field, no mutable collection;
//  3. the namespace OmniProduct_CoreDomain.ValueObjects.* holds records only: a class or a plain struct
//     declared there is a violation;
//  4. it pins the workshop's running example: a record named `Price` must exist under
//     OmniProduct_CoreDomain.ValueObjects - the concrete deliverable CUPID-step1-2.md asks for
//     (extract VAT out of Price, keep it composable and immutable).
public sealed class RecordsAreValueObjectsRule : IHarnessRule
{
    private const string ValueObjectNamespacePrefix = "OmniProduct_CoreDomain.ValueObjects";
    private const string EntityMarkerInterfaceName = "IDentifiable";
    private const string RequiredValueObjectTypeName = "Price";

    private static readonly HashSet<string> MutableCollectionTypes = new(StringComparer.Ordinal)
    {
        "List", "Dictionary", "HashSet", "Queue", "Stack", "LinkedList", "SortedList", "SortedDictionary"
    };

    public string Id => "HA3";
    public string Name => "Records are immutable Value Objects (never entities); ValueObjects holds records only; 'Price' must be one";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        var foundRequiredValueObject = false;

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol)
                    continue;

                var line = typeDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                if (typeDecl is RecordDeclarationSyntax)
                {
                    if (typeSymbol.AllInterfaces.Any(i => i.Name == EntityMarkerInterfaceName))
                    {
                        violations.Add(new Violation(
                            tree.FilePath,
                            line,
                            $"'{typeDecl.Identifier.Text}' is a record that implements {EntityMarkerInterfaceName}. Records are Value Objects, not entities - drop {EntityMarkerInterfaceName} or turn this into a class that follows HA9/HA10 instead."));
                    }

                    if (IsValueObject(typeSymbol))
                        violations.AddRange(CheckImmutable(tree, typeDecl, typeSymbol));

                    if (typeDecl.Identifier.Text == RequiredValueObjectTypeName && IsInValueObjectNamespace(typeSymbol))
                        foundRequiredValueObject = true;
                }
                else if (typeDecl is ClassDeclarationSyntax or StructDeclarationSyntax && IsInValueObjectNamespace(typeSymbol))
                {
                    violations.Add(new Violation(
                        tree.FilePath,
                        line,
                        $"'{typeDecl.Identifier.Text}' is a {typeDecl.Keyword.Text} under {ValueObjectNamespacePrefix}.*. Only records are allowed there - make it a record."));
                }
            }
        }

        if (!foundRequiredValueObject)
        {
            violations.Add(new Violation(
                trees.FirstOrDefault()?.FilePath ?? string.Empty,
                1,
                $"No record named '{RequiredValueObjectTypeName}' found under {ValueObjectNamespacePrefix}.*. '{RequiredValueObjectTypeName}' must be a record Value Object (see CUPID-step1-2.md)."));
        }

        return violations;
    }

    // Shared definition of "value object" (a record that is not an entity), reused by the HA4.x rules.
    internal static bool IsValueObject(INamedTypeSymbol symbol) =>
        symbol.IsRecord && symbol.AllInterfaces.All(i => i.Name != EntityMarkerInterfaceName);

    private static IEnumerable<Violation> CheckImmutable(SyntaxTree tree, TypeDeclarationSyntax typeDecl, INamedTypeSymbol symbol)
    {
        var line = typeDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        foreach (var member in symbol.GetMembers())
        {
            switch (member)
            {
                case IPropertySymbol { IsStatic: false } property:
                    if (property.SetMethod is { IsInitOnly: false })
                        yield return new Violation(tree.FilePath, line, $"Value object '{symbol.Name}' has a mutable 'set' on '{property.Name}'. Use 'init' or make it read-only.");
                    else if (IsMutableCollection(property.Type))
                        yield return new Violation(tree.FilePath, line, $"Value object '{symbol.Name}' exposes mutable collection '{property.Name}'. Use an immutable collection.");
                    break;

                case IFieldSymbol { IsStatic: false, IsReadOnly: false, IsImplicitlyDeclared: false } field:
                    yield return new Violation(tree.FilePath, line, $"Value object '{symbol.Name}' has a mutable field '{field.Name}'. Make it readonly.");
                    break;
            }
        }
    }

    private static bool IsMutableCollection(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsGenericType: true } named && MutableCollectionTypes.Contains(named.Name);

    private static bool IsInValueObjectNamespace(ITypeSymbol typeSymbol)
    {
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        return ns != null
               && (ns == ValueObjectNamespacePrefix || ns.StartsWith(ValueObjectNamespacePrefix + ".", StringComparison.Ordinal));
    }
}
