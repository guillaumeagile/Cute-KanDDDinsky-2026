using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA4.1 - CUPID Composable: extend through composition, not modification/inheritance.
// Any class/record that derives from another class/record (not an interface, not object/Exception)
// is flagged. Implementing interfaces is fine; subclassing is not.
public sealed class NoInheritanceRule : IHarnessRule
{
    public string Id => "HA4.1";
    public string Name => "Composable (no class/record inheritance, only interface implementation)";

    private static readonly HashSet<string> AllowedBaseTypes = new(StringComparer.Ordinal)
    {
        "Exception", "Attribute", "DbContext" // framework escape hatches, not domain modeling
    };

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
                var symbol = model.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                var baseType = symbol?.BaseType;

                if (baseType is null || baseType.SpecialType == SpecialType.System_Object)
                    continue;

                if (IsAllowedBase(baseType))
                    continue;

                violations.Add(new Violation(
                    tree.FilePath,
                    typeDecl.Identifier().GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    $"'{typeDecl.Identifier().Text}' inherits from '{baseType.Name}'. Extend through composition (wrap/delegate) instead of subclassing."));
            }
        }

        return violations;
    }

    private static bool IsAllowedBase(INamedTypeSymbol baseType)
    {
        for (var t = baseType; t is not null; t = t.BaseType)
        {
            if (AllowedBaseTypes.Contains(t.Name))
                return true;
        }

        return false;
    }
}

internal static class TypeDeclarationSyntaxExtensions
{
    public static SyntaxToken Identifier(this TypeDeclarationSyntax syntax) => syntax.Identifier;
}
