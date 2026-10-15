using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA2 - immutable data structures: enforce the usage of records and all immutable collections,
// no setter on any property.
public sealed class ImmutableDataStructuresRule : IHarnessRule
{
    public string Id => "HA2";
    public string Name => "Immutable data structures (records only, no setters, no mutable collections)";

    private static readonly HashSet<string> MutableCollectionTypes = new(StringComparer.Ordinal)
    {
        "List", "Dictionary", "HashSet", "Queue", "Stack", "LinkedList", "SortedList", "SortedDictionary"
    };

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            var root = tree.GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (IsInServicesNamespace(classDecl))
                    continue;

                violations.Add(new Violation(
                    tree.FilePath,
                    classDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    $"'{classDecl.Identifier.Text}' is a class, not a record. Domain types must be records."));
            }

            foreach (var prop in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                var setter = prop.AccessorList?.Accessors
                    .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));

                if (setter != null)
                {
                    violations.Add(new Violation(
                        tree.FilePath,
                        setter.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        $"Property '{prop.Identifier.Text}' has a mutable 'set' accessor. Use 'init' or make it read-only."));
                }
            }

            foreach (var genericName in root.DescendantNodes().OfType<GenericNameSyntax>())
            {
                if (MutableCollectionTypes.Contains(genericName.Identifier.Text) && !IsInServicesNamespace(genericName))
                {
                    violations.Add(new Violation(
                        tree.FilePath,
                        genericName.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        $"Uses mutable collection type '{genericName.Identifier.Text}<>'. Use an immutable collection (ImmutableList, ImmutableArray, IReadOnlyList, ...) instead."));
                }
            }
        }

        return violations;
    }

    // Services (namespace segment "Services") may be classes and mutable collections: they hold state, domain types may not.
    private static bool IsInServicesNamespace(SyntaxNode node)
    {
        var namespaceName = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;
        return namespaceName.Split('.').Contains("Services");
    }
}
