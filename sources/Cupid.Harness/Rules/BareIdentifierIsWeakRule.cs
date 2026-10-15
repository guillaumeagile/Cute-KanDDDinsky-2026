using Microsoft.CodeAnalysis;

namespace Cupid.Harness.Rules;

// HA12.1 (warning) - primitive obsession on identifiers. Any `Id` / `*Id` / `*Ids` property, field, local,
// return type or parameter (services included) whose type is not declared in the analysed source is a
// BCL / package type used raw: string, int, Guid, Ulid, Uri, ... A domain record such as
// record ProductId(Guid Value) is declared in source and passes (HA12 checks that it wraps a Guid/Ulid).
// For collections and dictionaries named `*Ids`, the element and key types are checked.
public sealed class BareIdentifierIsWeakRule : IHarnessRule
{
    public string Id => "HA12.1";
    public string Name => "Identifiers should be domain records, not raw BCL types such as string/int/Guid (warning)";
    public HarnessSeverity Severity => HarnessSeverity.Warning;

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation) =>
        IdentifierShape.FindIdMembers(trees, compilation, includeServices: true)
            .SelectMany(m => IdentifierShape.Leaves(m)
                .Where(t => !IdentifierShape.IsDomainType(t))
                .Select(t =>
                {
                    var type = t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    var wrapper = m.Name is "Id" or "id" ? m.Owner + "Id" : char.ToUpperInvariant(m.Name[0]) + m.Name[1..];
                    return new Violation(m.Tree.FilePath, IdentifierShape.Line(m.Node),
                        $"'{m.Owner}.{m.Name}' is a bare {type}. Wrap it in a domain record, e.g. record {wrapper}(Guid Value).");
                }))
            .ToList();
}
