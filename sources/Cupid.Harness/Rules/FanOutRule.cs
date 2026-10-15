using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA7 - Fan-out (Unix Philosophy proxy), strict form: applies only to ENTITIES and an entity may
// reference NO other entity type at all (default cap = 0). Entities relate to each other by
// identifier only (e.g. a WarehouseId), never by holding or accepting the other entity object:
// not via fields, properties, method/constructor parameters, return types, locals, or generic
// arguments (List<Supplier>, Dictionary<string, Supplier>, Supplier[], Supplier?).
//
// Two distinct roles, on purpose:
//  - REFERENCED entity: a class under OmniProduct_CoreDomain.Models.* that implements IDentifiable.
//    Value objects (Price, records, anything without IDentifiable) are freely referenceable.
//  - CHECKED type (subject): any non-record class under Models.* except DbContext subclasses,
//    whether or not it implements IDentifiable. A type that simply omits the interface must not
//    dodge the rule (HA9 reports the omission separately; HA7 must not depend on it being fixed).
// Types are matched by symbol identity, not by simple name.
//
// Every offending reference is reported with its own file:line, the member it sits in, and the
// entity it points at, so the diagnostic says exactly what to replace with an id.
public sealed class FanOutRule : IHarnessRule
{
    private const string EntityMarkerInterfaceName = "IDentifiable";
    private const string EntityNamespacePrefix = "OmniProduct_CoreDomain.Models";

    private readonly int _maxDistinctDomainTypes;

    public FanOutRule(int maxDistinctDomainTypes = 0)
    {
        _maxDistinctDomainTypes = maxDistinctDomainTypes;
    }

    public string Id => "HA7";
    public string Name => $"Fan-out (an entity references at most {_maxDistinctDomainTypes} other entity type(s); reference by id instead)";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var declared = model.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (declared == null || !IsCheckedType(declared))
                    continue; // HA7 only applies to entities; services/value objects are out of scope.

                // (line, entity name, member) - a set so one type token counted via two paths reports once.
                var found = new SortedSet<(int Line, string Entity, string Member)>();

                foreach (var node in typeDecl.DescendantNodes())
                {
                    if (node is not (IdentifierNameSyntax or GenericNameSyntax))
                        continue;

                    var info = model.GetSymbolInfo(node);
                    var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                    var type = symbol switch
                    {
                        ITypeSymbol t => t,
                        ILocalSymbol l => l.Type,
                        IParameterSymbol p => p.Type,
                        IFieldSymbol f => f.Type,
                        IPropertySymbol pr => pr.Type,
                        IMethodSymbol { MethodKind: MethodKind.Constructor } c => c.ContainingType,
                        _ => null
                    };

                    foreach (var entity in EntitiesIn(type))
                    {
                        if (SymbolEqualityComparer.Default.Equals(entity, declared))
                            continue;

                        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        found.Add((line, entity.Name, DescribeMember(node, typeDecl)));
                    }
                }

                var distinct = found.Select(f => f.Entity).Distinct().Count();
                if (distinct <= _maxDistinctDomainTypes)
                    continue;

                foreach (var (line, entity, member) in found)
                {
                    violations.Add(new Violation(
                        tree.FilePath,
                        line,
                        $"'{typeDecl.Identifier.Text}' references entity '{entity}' in {member}. " +
                        $"Entities must not reference other entities - hold its identifier instead (e.g. {entity}Id). " +
                        $"[{distinct} distinct entity type(s) referenced by '{typeDecl.Identifier.Text}', max {_maxDistinctDomainTypes}]"));
                }
            }
        }

        return violations;
    }

    private static string DescribeMember(SyntaxNode node, TypeDeclarationSyntax owner)
    {
        var member = node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(m => m != owner);
        return member switch
        {
            PropertyDeclarationSyntax p => $"property '{p.Identifier.Text}'",
            FieldDeclarationSyntax f => $"field '{f.Declaration.Variables.First().Identifier.Text}'",
            MethodDeclarationSyntax m => $"method '{m.Identifier.Text}'",
            ConstructorDeclarationSyntax => "a constructor",
            null => "the type declaration",
            _ => member.Kind().ToString()
        };
    }

    // Unwraps arrays, nullable and generic arguments so List<Supplier> counts as Supplier.
    private static IEnumerable<INamedTypeSymbol> EntitiesIn(ITypeSymbol? type)
    {
        switch (type)
        {
            case null:
                yield break;
            case IArrayTypeSymbol array:
                foreach (var e in EntitiesIn(array.ElementType)) yield return e;
                break;
            case INamedTypeSymbol named:
                if (IsEntity(named)) yield return named.OriginalDefinition;
                foreach (var arg in named.TypeArguments)
                    foreach (var e in EntitiesIn(arg)) yield return e;
                break;
        }
    }

    private static bool IsCheckedType(ITypeSymbol typeSymbol)
    {
        return typeSymbol is { TypeKind: TypeKind.Class, IsRecord: false, IsStatic: false }
               && IsInEntityNamespace(typeSymbol)
               && !IsPersistenceInfrastructure(typeSymbol);
    }

    private static bool IsEntity(ITypeSymbol typeSymbol)
    {
        return IsCheckedType(typeSymbol) && typeSymbol.AllInterfaces.Any(i => i.Name == EntityMarkerInterfaceName);
    }

    // EF contexts live next to the models but are infrastructure, not entities. EF isn't referenced
    // by the harness compilation so the base type may be an error type; its name is still available.
    private static bool IsPersistenceInfrastructure(ITypeSymbol typeSymbol)
    {
        for (var b = typeSymbol.BaseType; b != null; b = b.BaseType)
        {
            if (b.Name == "DbContext")
                return true;
        }

        return false;
    }

    private static bool IsInEntityNamespace(ITypeSymbol typeSymbol)
    {
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        return ns != null
               && (ns == EntityNamespacePrefix || ns.StartsWith(EntityNamespacePrefix + ".", StringComparison.Ordinal));
    }
}
