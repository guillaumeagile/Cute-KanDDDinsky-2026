using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// Shared by HA12 / HA12.1: finds the identifier-like members (`Id`, `*Id`) of the model and classifies
// their type. Names are matched, not namespaces, except that `Services` namespaces are out of scope.
internal static class IdentifierShape
{
    private const string EntityMarkerInterfaceName = "IDentifiable";

    internal sealed record IdMember(SyntaxTree Tree, SyntaxNode Node, string Owner, string Name, ITypeSymbol Type);

    // `Id`, `id`, `*Id` (single id) and `*Ids` (collection of ids).
    internal static bool IsIdName(string name) =>
        name is "Id" or "id" or "Ids" or "ids"
        || (name.Length > 2 && name.EndsWith("Id", StringComparison.Ordinal))
        || (name.Length > 3 && name.EndsWith("Ids", StringComparison.Ordinal));

    internal static bool IsGuidOrUlid(ITypeSymbol type) => type.Name is "Guid" or "Ulid";

    // A domain type: declared in the analysed source (not the BCL / a NuGet package) and not a stand-in for Guid/Ulid.
    internal static bool IsDomainType(ITypeSymbol type) =>
        !IsGuidOrUlid(type) && type.Locations.Any(l => l.IsInSource);

    // The type(s) that actually carry the identifier: unwraps Nullable<T> and arrays; for a plural `*Ids`
    // member also every type argument of a generic collection or dictionary (element and key).
    internal static IEnumerable<ITypeSymbol> Leaves(IdMember member) => Leaves(member.Type, member.Name);

    private static IEnumerable<ITypeSymbol> Leaves(ITypeSymbol type, string name)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return Leaves(array.ElementType, name);
            case INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable:
                return Leaves(nullable.TypeArguments[0], name);
            case INamedTypeSymbol { IsGenericType: true } generic when name.EndsWith("Ids", StringComparison.OrdinalIgnoreCase):
                return generic.TypeArguments.SelectMany(t => Leaves(t, name));
            default:
                return new[] { type };
        }
    }

    // A record whose only public instance property is a Guid or a Ulid, e.g. record ProductId(Guid Value).
    internal static bool IsWrapper(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { IsRecord: true } record)
            return false;

        var properties = record.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public)
            .ToList();
        return properties.Count == 1 && IsGuidOrUlid(properties[0].Type);
    }

    internal static bool InServices(ISymbol? symbol)
    {
        var ns = symbol?.ContainingNamespace?.ToDisplayString();
        return ns != null && ns.Split('.').Contains("Services");
    }

    internal static IEnumerable<IdMember> FindIdMembers(
        IReadOnlyList<SyntaxTree> trees, Compilation compilation, bool includeServices)
    {
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                string name;
                switch (node)
                {
                    case PropertyDeclarationSyntax p: name = p.Identifier.Text; break;
                    case ParameterSyntax { Type: not null } p: name = p.Identifier.Text; break;
                    case VariableDeclaratorSyntax v: name = v.Identifier.Text; break;
                    case MethodDeclarationSyntax m: name = m.Identifier.Text; break;
                    case LocalFunctionStatementSyntax l: name = l.Identifier.Text; break;
                    default: continue;
                }

                if (!IsIdName(name))
                    continue;

                var symbol = model.GetDeclaredSymbol(node);
                var type = symbol switch
                {
                    IPropertySymbol p => p.Type,
                    IParameterSymbol p => p.Type,
                    IFieldSymbol f => f.Type,
                    ILocalSymbol l => l.Type,
                    IMethodSymbol { ReturnsVoid: false } m => m.ReturnType,
                    _ => null
                };

                if (symbol == null || type == null || type.TypeKind == TypeKind.Error && type.Name.Length == 0)
                    continue;
                if (!includeServices && InServices(symbol))
                    continue;

                yield return new IdMember(tree, node, symbol.ContainingType?.Name ?? string.Empty, name, type);
            }
        }
    }

    // Creation shapes that cannot be UUID v7+ / a Ulid: Guid.NewGuid() (v4), Guid.Empty, new Guid(), default(Guid).
    internal static IEnumerable<(SyntaxTree Tree, SyntaxNode Node, string Text)> FindBadCreations(
        IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                string? text = node switch
                {
                    MemberAccessExpressionSyntax m when model.GetSymbolInfo(m).Symbol is { ContainingType.Name: "Guid" } s
                        && s.Name == "NewGuid" => "Guid.NewGuid() creates a UUID v4",
                    MemberAccessExpressionSyntax m when model.GetSymbolInfo(m).Symbol is { ContainingType.Name: "Guid" } s
                        && s.Name == "Empty" => "Guid.Empty is an all-zero identifier, not a UUID v7",
                    ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0 } o
                        when model.GetTypeInfo(o).Type is { Name: "Guid" } => "new Guid() is an all-zero identifier, not a UUID v7",
                    ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0 } o
                        when model.GetTypeInfo(o).Type is { Name: "Guid" } => "new() Guid is an all-zero identifier, not a UUID v7",
                    DefaultExpressionSyntax d when model.GetTypeInfo(d).Type is { Name: "Guid" } => "default(Guid) is an all-zero identifier, not a UUID v7",
                    _ => null
                };

                if (text == null || !IsIdContext(node, model))
                    continue;

                yield return (tree, node, text);
            }
        }
    }

    private static bool IsIdContext(SyntaxNode node, SemanticModel model)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case PropertyDeclarationSyntax p when IsIdName(p.Identifier.Text):
                case VariableDeclaratorSyntax v when IsIdName(v.Identifier.Text):
                case ArgumentSyntax { NameColon.Name.Identifier.Text: var arg } when IsIdName(arg):
                    return true;

                case AssignmentExpressionSyntax a when TargetName(a.Left) is { } name && IsIdName(name):
                    return true;

                case TypeDeclarationSyntax t when model.GetDeclaredSymbol(t) is INamedTypeSymbol type
                    && (InServices(type) ? false : type.AllInterfaces.Any(i => i.Name == EntityMarkerInterfaceName) || IsWrapper(type)):
                    return true;
            }
        }

        return false;
    }

    private static string? TargetName(ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax i => i.Identifier.Text,
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        _ => null
    };

    internal static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
