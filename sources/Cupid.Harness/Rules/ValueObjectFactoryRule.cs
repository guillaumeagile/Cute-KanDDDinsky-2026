using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA4.4 - Composable, step 3 (heuristic): input is validated at the boundary, not by throwing in a constructor.
// At least one value object must expose a static `Create` or `Build` method returning OneOf<Self, E>, where
// Self is the value object itself and E implements IValidationError (an interface: HA4 forbids base classes).
public sealed class ValueObjectFactoryRule : IHarnessRule
{
    private const string ValidationErrorInterfaceName = "IValidationError";
    private static readonly string[] FactoryNames = ["Create", "Build"];

    public string Id => "HA4.4";
    public string Name => $"A value object has a Create/Build returning OneOf<Self, E> with E : {ValidationErrorInterfaceName}";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var recordDecl in tree.GetRoot().DescendantNodes().OfType<RecordDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(recordDecl) is INamedTypeSymbol symbol
                    && RecordsAreValueObjectsRule.IsValueObject(symbol)
                    && symbol.GetMembers().OfType<IMethodSymbol>().Any(m => IsValidatingFactory(symbol, m)))
                    return [];
            }
        }

        return
        [
            new Violation(
                trees.FirstOrDefault()?.FilePath ?? string.Empty,
                1,
                $"No value object has a static Create/Build returning OneOf<Self, E> where E implements {ValidationErrorInterfaceName}. Validate input at the boundary instead of throwing in a constructor.")
        ];
    }

    private static bool IsValidatingFactory(INamedTypeSymbol valueObject, IMethodSymbol method) =>
        method is { IsStatic: true, MethodKind: MethodKind.Ordinary }
        && FactoryNames.Contains(method.Name)
        && method.ReturnType is INamedTypeSymbol
        {
            IsGenericType: true,
            TypeArguments: [var success, var error]
        } returned
        && returned.OriginalDefinition.Name == "OneOf"
        && returned.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "OneOf"
        && SymbolEqualityComparer.Default.Equals(success, valueObject)
        && error.AllInterfaces.Any(i => i.Name == ValidationErrorInterfaceName);
}
