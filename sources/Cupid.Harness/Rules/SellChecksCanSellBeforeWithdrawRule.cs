using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA4.7 (warning only) - Composable, step 7, simplified: in any method named `SellProduct`, a call to
// `CanSell` must come before the first call to `Withdraw`, so the sale is refused before stock is touched.
// Name-based on purpose (light heuristic). A SellProduct that never calls Withdraw is not judged.
public sealed class SellChecksCanSellBeforeWithdrawRule : IHarnessRule
{
    private const string SellMethodName = "SellProduct";

    public string Id => "HA4.7";
    public string Name => "SellProduct calls CanSell before Withdraw (warning)";
    public HarnessSeverity Severity => HarnessSeverity.Warning;

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                         .Where(m => m.Identifier.Text == SellMethodName))
            {
                var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Select(i => (Name: CalledName(i), i.SpanStart, Node: i))
                    .OrderBy(c => c.SpanStart)
                    .ToList();

                var withdraw = calls.FirstOrDefault(c => c.Name == "Withdraw");
                if (withdraw.Node is null)
                    continue;

                if (!calls.Any(c => c.Name == "CanSell" && c.SpanStart < withdraw.SpanStart))
                {
                    violations.Add(new Violation(
                        tree.FilePath,
                        withdraw.Node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        $"'{SellMethodName}' calls Withdraw without calling CanSell first. Check CanSell before touching the stock."));
                }
            }
        }

        return violations;
    }

    private static string CalledName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => string.Empty
    };
}
