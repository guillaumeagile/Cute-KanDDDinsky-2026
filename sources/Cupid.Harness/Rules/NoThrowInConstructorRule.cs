using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA4.2 - Composable, step 3 follow-up: a constructor must not throw. Expected failures (bad input) are returned
// as values by a factory (see HA4.4: OneOf<Self, IValidationError>), not raised from `new`.
// Flags `throw` statements, `throw` expressions (`x ?? throw ...`) and `ThrowIf*` guard calls (e.g.
// ArgumentNullException.ThrowIfNull) inside any constructor body, expression body or initializer.
public sealed class NoThrowInConstructorRule : IHarnessRule
{
    public string Id => "HA4.2";
    public string Name => "No exception thrown in constructors (return a OneOf from a factory instead)";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            foreach (var ctor in tree.GetRoot().DescendantNodes().OfType<ConstructorDeclarationSyntax>())
            {
                var typeName = (ctor.Parent as TypeDeclarationSyntax)?.Identifier.Text ?? ctor.Identifier.Text;

                foreach (var node in ctor.DescendantNodes())
                {
                    var what = node switch
                    {
                        ThrowStatementSyntax => "a 'throw' statement",
                        ThrowExpressionSyntax => "a 'throw' expression",
                        InvocationExpressionSyntax invocation when IsThrowGuard(invocation) => "a 'ThrowIf*' guard call",
                        _ => null
                    };

                    if (what is null)
                        continue;

                    violations.Add(new Violation(
                        tree.FilePath,
                        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        $"Constructor of '{typeName}' contains {what}. Validate in a Create/Build factory returning OneOf<{typeName}, IValidationError> instead."));
                }
            }
        }

        return violations;
    }

    private static bool IsThrowGuard(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => string.Empty
        };

        return name.StartsWith("ThrowIf", StringComparison.Ordinal);
    }
}
