using Microsoft.CodeAnalysis;

namespace Cupid.Harness.Rules;

// HA12 - Identifiers are rich objects: every `Id` / `*Id` / `*Ids` property, field, local, return type or parameter of the model (anything
// outside a `Services` namespace) is a Guid or a Ulid, or a record wrapping exactly one of them
// (record ProductId(Guid Value)). Never string / int / long. "At least v7" is checked by shape only:
// Guid.NewGuid() (v4), Guid.Empty, new Guid() and default(Guid) are flagged in id context, while
// Guid.CreateVersion7() / Ulid.NewUlid() are fine. A bare Guid/Ulid passes here and is a warning in HA12.1.
public sealed class RichIdentifierRule : IHarnessRule
{
    public string Id => "HA12";
    public string Name => "Identifiers are rich objects: Guid/Ulid (or a record wrapping one), UUID v7 or later";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var member in IdentifierShape.FindIdMembers(trees, compilation, includeServices: false))
        {
            foreach (var type in IdentifierShape.Leaves(member))
            {
                if (IdentifierShape.IsGuidOrUlid(type) || IdentifierShape.IsWrapper(type))
                    continue;

                violations.Add(new Violation(member.Tree.FilePath, IdentifierShape.Line(member.Node),
                    $"'{member.Owner}.{member.Name}' is typed '{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}'. Identifiers must be rich objects: a record wrapping a Guid (UUID v7) or a Ulid."));
            }
        }

        foreach (var (tree, node, text) in IdentifierShape.FindBadCreations(trees, compilation))
        {
            violations.Add(new Violation(tree.FilePath, IdentifierShape.Line(node),
                $"{text}. Identifiers must be UUID v7 or later: use Guid.CreateVersion7() or Ulid.NewUlid()."));
        }

        return violations;
    }
}
