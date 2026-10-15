using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA8 - No Persistence in Domain: a domain model should not know how it is stored. This flags the
// exact smell CUPID-step1-1's Concept 1.1 targets in Product.cs: EF Core data-annotation
// attributes on domain types, hand-rolled "shadow property" fields that flatten a real domain
// value for the ORM (*Csv, *Json, *Amount/*Currency pairs mirroring a richer type), and
// sync/hydrate methods that exist only to keep those shadows in agreement with the real fields.
//
// This is deliberately narrower than "no [Table]/[Column] anywhere" - a dedicated persistence/
// infrastructure type (e.g. a DbContext, a mapping profile) is expected to know about storage.
// The rule only looks at types that are NOT themselves EF infrastructure (DbContext subclasses)
// and flags persistence vocabulary found on them.
public sealed class NoPersistenceInDomainRule : IHarnessRule
{
    private static readonly HashSet<string> EfCoreAttributes = new(StringComparer.Ordinal)
    {
        "Table", "Column", "Key", "NotMapped", "ForeignKey", "DatabaseGenerated", "Required", "MaxLength", "InverseProperty"
    };

    private static readonly HashSet<string> PersistenceMethodPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SyncEfColumns", "HydrateFromEfColumns", "Sync", "Hydrate", "ToEntity", "FromEntity", "MapToEntity", "MapFromEntity"
    };

    private static readonly string[] PersistenceShadowPropertySuffixes = { "Csv", "Json" };

    public string Id => "HA8";
    public string Name => "No Persistence in Domain (no EF/ORM vocabulary on domain models)";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            var typeDecls = tree.GetRoot().DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(t => t is ClassDeclarationSyntax or RecordDeclarationSyntax);

            foreach (var typeDecl in typeDecls)
            {
                if (IsEfInfrastructureType(typeDecl, model))
                    continue; // a DbContext (or similar) is expected to know about persistence.

                CheckAttributes(tree, typeDecl, violations);
                CheckShadowProperties(tree, typeDecl, violations);
                CheckPersistenceMethods(tree, typeDecl, violations);
            }
        }

        return violations;
    }

    private static bool IsEfInfrastructureType(TypeDeclarationSyntax typeDecl, SemanticModel model)
    {
        var symbol = model.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        for (var t = symbol?.BaseType; t is not null; t = t.BaseType)
        {
            if (t.Name == "DbContext")
                return true;
        }
        return false;
    }

    private static void CheckAttributes(SyntaxTree tree, TypeDeclarationSyntax typeDecl, List<Violation> violations)
    {
        foreach (var member in typeDecl.Members)
        {
            foreach (var attrList in member.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString().Replace("Attribute", "");
                    if (EfCoreAttributes.Contains(name))
                    {
                        violations.Add(new Violation(
                            tree.FilePath,
                            attr.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            $"'{typeDecl.Identifier.Text}' uses EF Core attribute '[{name}]' directly on the domain model. Move persistence mapping into the DbContext/infrastructure layer (fluent API or a separate mapping type)."));
                    }
                }
            }
        }
    }

    private static void CheckShadowProperties(SyntaxTree tree, TypeDeclarationSyntax typeDecl, List<Violation> violations)
    {
        foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (PersistenceShadowPropertySuffixes.Any(suffix => prop.Identifier.Text.EndsWith(suffix, StringComparison.Ordinal)))
            {
                violations.Add(new Violation(
                    tree.FilePath,
                    prop.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    $"'{typeDecl.Identifier.Text}.{prop.Identifier.Text}' looks like a hand-flattened ORM shadow property. The domain type should expose the real value only; let the persistence layer handle the flattening/serialization."));
            }
        }
    }

    private static void CheckPersistenceMethods(SyntaxTree tree, TypeDeclarationSyntax typeDecl, List<Violation> violations)
    {
        foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            if (PersistenceMethodPrefixes.Any(prefix => method.Identifier.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add(new Violation(
                    tree.FilePath,
                    method.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    $"'{typeDecl.Identifier.Text}.{method.Identifier.Text}' is a persistence-mapping method living on the domain model. Move ORM sync/hydrate logic into the infrastructure layer (e.g. a value converter or repository mapping)."));
            }
        }
    }
}
