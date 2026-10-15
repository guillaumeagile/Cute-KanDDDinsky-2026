using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA4.6 - Composable, step 6: the model never reads the clock. Time is passed in (`today`) or comes from an
// injected TimeProvider, so each date case can be tested without waiting.
// "Model" = entities (types implementing IDentifiable) and value objects (records that are not IDentifiable);
// services are out of scope. Flags DateTime.Now/UtcNow/Today, DateTimeOffset.Now/UtcNow and TimeProvider.System
// (an injected TimeProvider instance is fine). Detection is semantic, so `using static` and aliases are caught.
public sealed class NoClockInModelRule : IHarnessRule
{
    private const string EntityMarkerInterfaceName = "IDentifiable";

    private static readonly Dictionary<string, HashSet<string>> ClockProperties = new(StringComparer.Ordinal)
    {
        ["System.DateTime"] = ["Now", "UtcNow", "Today"],
        ["System.DateTimeOffset"] = ["Now", "UtcNow"],
        ["System.TimeProvider"] = ["System"],
    };

    public string Id => "HA4.6";
    public string Name => "No clock read in the model (pass 'today' or inject a TimeProvider)";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var identifier in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (model.GetSymbolInfo(identifier).Symbol is not IPropertySymbol { IsStatic: true } property
                    || !IsClockProperty(property))
                    continue;

                var enclosing = identifier.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                if (enclosing is null
                    || model.GetDeclaredSymbol(enclosing) is not INamedTypeSymbol type
                    || !IsModelType(type))
                    continue;

                violations.Add(new Violation(
                    tree.FilePath,
                    identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    $"'{type.Name}' reads the clock via '{property.ContainingType.Name}.{property.Name}'. Pass 'today' as a parameter or inject a TimeProvider."));
            }
        }

        return violations;
    }

    private static bool IsClockProperty(IPropertySymbol property) =>
        ClockProperties.TryGetValue(property.ContainingType.ToDisplayString(), out var names)
        && names.Contains(property.Name);

    private static bool IsModelType(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(i => i.Name == EntityMarkerInterfaceName)
        || RecordsAreValueObjectsRule.IsValueObject(type);
}
