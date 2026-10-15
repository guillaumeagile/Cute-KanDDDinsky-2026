using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA4.5 - Composable, step 4 (light): behaviour per alternative goes through OneOf.Match, never through type tests.
// Two checks:
//   1. no `is` / `as` / `switch` on a OneOf, nor testing for one of its alternatives (a type argument of any
//      OneOf<...> used in the source);
//   2. `.Match(...)` is actually called on at least one OneOf<Y, Z, ...> whose type arguments are all records.
public sealed class MatchOverOneOfRule : IHarnessRule
{
    public string Id => "HA4.5";
    public string Name => "Alternatives are handled with OneOf.Match over records, never is/as/switch";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var models = trees.ToDictionary(t => t, t => compilation.GetSemanticModel(t));
        var alternatives = CollectAlternatives(trees, models);
        var nodes = trees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().Select(node => (tree, node, model: models[tree])))
            .ToList();

        var typeTests = nodes
            .Select(x => (x.tree, x.node, kind: TypeTestKind(x.node, x.model, alternatives)))
            .Where(x => x.kind is not null)
            .Select(x => new Violation(
                x.tree.FilePath,
                x.node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                $"'{x.kind}' on a OneOf or one of its alternatives. Handle each alternative with .Match(...) instead."));

        var matchOverRecordsFound = nodes.Any(x => x.node is InvocationExpressionSyntax invocation && IsMatchOverRecords(invocation, x.model));

        return typeTests
            .Concat(matchOverRecordsFound
                ? []
                : [new Violation(
                    trees.FirstOrDefault()?.FilePath ?? string.Empty,
                    1,
                    "No .Match(...) call on a OneOf<...> whose type arguments are all records. Handle the alternatives with Match.")])
            .ToList();
    }

    private static string? TypeTestKind(SyntaxNode node, SemanticModel model, HashSet<ITypeSymbol> alternatives)
    {
        string? Tests(string kind, ExpressionSyntax operand, IEnumerable<TypeSyntax> testedTypes) =>
            IsOneOf(model.GetTypeInfo(operand).Type)
            || testedTypes.Any(t => model.GetTypeInfo(t).Type is { } type && alternatives.Contains(type))
                ? kind
                : null;

        return node switch
        {
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression) || binary.IsKind(SyntaxKind.AsExpression) =>
                Tests(binary.IsKind(SyntaxKind.IsExpression) ? "is" : "as", binary.Left, new[] { binary.Right }.OfType<TypeSyntax>()),
            IsPatternExpressionSyntax isPattern => Tests("is", isPattern.Expression, PatternTypes(isPattern.Pattern)),
            SwitchStatementSyntax statement => Tests("switch", statement.Expression, statement.Sections
                .SelectMany(s => s.Labels.OfType<CasePatternSwitchLabelSyntax>())
                .SelectMany(l => PatternTypes(l.Pattern))),
            SwitchExpressionSyntax expression => Tests("switch", expression.GoverningExpression, expression.Arms.SelectMany(a => PatternTypes(a.Pattern))),
            _ => null
        };
    }

    private static IEnumerable<TypeSyntax> PatternTypes(PatternSyntax pattern) =>
        pattern.DescendantNodesAndSelf().SelectMany<SyntaxNode, TypeSyntax>(n => n switch
        {
            DeclarationPatternSyntax d => [d.Type],
            TypePatternSyntax t => [t.Type],
            ConstantPatternSyntax { Expression: TypeSyntax name } => [name], // `Perishable => ...` parses as a constant pattern
            RecursivePatternSyntax { Type: { } type } => [type],
            _ => []
        });

    private static HashSet<ITypeSymbol> CollectAlternatives(IReadOnlyList<SyntaxTree> trees, Dictionary<SyntaxTree, SemanticModel> models) =>
        trees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<GenericNameSyntax>()
                .Select(generic => models[tree].GetSymbolInfo(generic).Symbol))
            .OfType<INamedTypeSymbol>()
            .Where(IsOneOf)
            .SelectMany(type => type.TypeArguments)
            .ToHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

    // Resolved from the receiver type, not the method symbol: the latter is null when the OneOf assembly's own
    // references (netstandard) are not part of the compilation.
    private static bool IsMatchOverRecords(InvocationExpressionSyntax invocation, SemanticModel model) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Match" } access
        && model.GetTypeInfo(access.Expression).Type is INamedTypeSymbol receiver
        && IsOneOf(receiver)
        && receiver.TypeArguments.All(a => a.IsRecord);

    private static bool IsOneOf(ITypeSymbol? type) =>
        type is INamedTypeSymbol { IsGenericType: true } named
        && named.OriginalDefinition.Name == "OneOf"
        && named.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "OneOf";
}
