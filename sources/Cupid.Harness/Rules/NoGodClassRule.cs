using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA5 - no god class: no class shall have more than 6 public properties.
// Properties (and positional record parameters) whose name ends with "Id" are ignored - they're
// mandatory identifiers/pivot properties, not part of the type's behavioral surface.
public sealed class NoGodClassRule : IHarnessRule
{
    private readonly int _maxPublicProperties;

    public NoGodClassRule(int maxPublicProperties = 4)
    {
        _maxPublicProperties = maxPublicProperties;
    }

    public string Id => "HA5";
    public string Name => $"No god class (at most {_maxPublicProperties} public properties)";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            var typeDecls = tree.GetRoot().DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(t => t is ClassDeclarationSyntax or RecordDeclarationSyntax);

            foreach (var typeDecl in typeDecls)
            {
                var publicPropertyCount = typeDecl.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .Count(p => p.Modifiers.Any(m => m.Text == "public") && !IsIdProperty(p.Identifier.Text));

                // Positional record parameters are public properties too.
                publicPropertyCount += typeDecl.ParameterList?.Parameters
                    .Count(p => !IsIdProperty(p.Identifier.Text)) ?? 0;

                if (publicPropertyCount > _maxPublicProperties)
                {
                    violations.Add(new Violation(
                        tree.FilePath,
                        typeDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        $"'{typeDecl.Identifier.Text}' has {publicPropertyCount} public properties (max {_maxPublicProperties}). Split into smaller, focused types."));
                }
            }
        }

        return violations;
    }

    private static bool IsIdProperty(string name) => name.EndsWith("Id", StringComparison.Ordinal);
}
