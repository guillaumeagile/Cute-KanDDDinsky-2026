using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA9 - Entities must implement IDentifiable: closes the loophole HA7 would otherwise have. HA7
// only counts fan-out toward types that implement IDentifiable, so a type that lives in the
// entity namespace (OmniProduct_CoreDomain.Models.*) but skips the interface would silently dodge
// the fan-out cap. This rule makes that namespace/interface pairing mandatory: every class or
// record declared under OmniProduct_CoreDomain.Models (or a nested namespace under it) must
// implement IDentifiable, so "is this an entity?" has one answer, not two.
public sealed class EntityMustImplementIdentifiableRule : IHarnessRule
{
    private const string EntityNamespacePrefix = "OmniProduct_CoreDomain.Models";
    private const string EntityMarkerInterfaceName = "IDentifiable";

    public string Id => "HA9";
    public string Name => $"Entities must implement {EntityMarkerInterfaceName} (types under {EntityNamespacePrefix}.*)";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            var typeDecls = tree.GetRoot().DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(t => t is ClassDeclarationSyntax or RecordDeclarationSyntax);

            foreach (var typeDecl in typeDecls)
            {
                var typeSymbol = model.GetDeclaredSymbol(typeDecl) as ITypeSymbol;
                if (typeSymbol == null)
                    continue;

                if (!IsInEntityNamespace(typeSymbol))
                    continue;

                if (typeSymbol.AllInterfaces.Any(i => i.Name == EntityMarkerInterfaceName))
                    continue;

                violations.Add(new Violation(
                    tree.FilePath,
                    typeDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    $"'{typeSymbol.ToDisplayString()}' lives under {EntityNamespacePrefix}.* but does not implement {EntityMarkerInterfaceName}. Either implement {EntityMarkerInterfaceName} (it's an entity) or move it out of the Models namespace (it's a value object/service)."));
            }
        }

        return violations;
    }

    private static bool IsInEntityNamespace(ITypeSymbol typeSymbol)
    {
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        return ns != null
               && (ns == EntityNamespacePrefix || ns.StartsWith(EntityNamespacePrefix + ".", StringComparison.Ordinal));
    }
}
