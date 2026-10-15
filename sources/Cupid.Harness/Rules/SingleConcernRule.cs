using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA6 - Single Concern (Unix Philosophy: "do one thing well"). Groups a class's public method
// names by the bounded-context concern they belong to (see ubiquitous-language-map.json). A class
// whose public methods span more than one concern is mixing responsibilities - exactly the
// ProductService/Product smell from CUPID-step1-1's Concept 1.1 ("catalog, pricing, stock,
// supplier notification, and transport - all at once").
//
// Also enforces the map's "restrictions" section: a per-class-name list of nouns/verbs that must
// never appear in that specific type's public method or property names, even if they'd otherwise
// be allowed vocabulary elsewhere. This targets vocabulary that was deliberately extracted out of a
// type (e.g. Product no longer owns Catalog/Pricing/Supplier behavior after it was split into
// ProductCatalog/ProductPricing/ProductSuppliers) and should never creep back in.
public sealed class SingleConcernRule : IHarnessRule
{
    private readonly UbiquitousLanguageMap _map;
    private readonly int _maxConcernsPerClass;

    public SingleConcernRule(UbiquitousLanguageMap map, int maxConcernsPerClass = 1)
    {
        _map = map;
        _maxConcernsPerClass = maxConcernsPerClass;
    }

    public string Id => "HA6";
    public string Name => $"Single Concern (public methods span at most {_maxConcernsPerClass} bounded-context concern(s))";

    public IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation)
    {
        var violations = new List<Violation>();

        foreach (var tree in trees)
        {
            var root = tree.GetRoot();

            var typeDecls = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(t => t is ClassDeclarationSyntax or RecordDeclarationSyntax);

            foreach (var typeDecl in typeDecls)
            {
                var publicMethods = typeDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(mod => mod.Text == "public"))
                    .ToList();

                CheckConcerns(tree, typeDecl, publicMethods, violations);
                CheckRestrictions(tree, typeDecl, violations);
            }
        }

        return violations;
    }

    private void CheckConcerns(
        SyntaxTree tree, TypeDeclarationSyntax typeDecl, List<MethodDeclarationSyntax> publicMethods, List<Violation> violations)
    {
        var methodsByConcern = publicMethods
            .SelectMany(m => _map.ConcernsFor(m.Identifier.Text).Select(concern => (Concern: concern, Method: m)))
            .ToList();

        var distinctConcerns = methodsByConcern
            .Select(x => x.Concern)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (distinctConcerns.Count > _maxConcernsPerClass)
        {
            var detail = string.Join(", ", distinctConcerns.Select(concern =>
                $"{concern} ({string.Join("/", methodsByConcern.Where(x => x.Concern == concern).Select(x => x.Method.Identifier.Text))})"));

            violations.Add(new Violation(
                tree.FilePath,
                typeDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                $"'{typeDecl.Identifier.Text}' mixes {distinctConcerns.Count} concerns: {detail}. Extract one class per concern."));
        }
    }

    private void CheckRestrictions(SyntaxTree tree, TypeDeclarationSyntax typeDecl, List<Violation> violations)
    {
        var typeName = typeDecl.Identifier.Text;

        var publicMembers = typeDecl.Members
            .Where(m => m is MethodDeclarationSyntax or PropertyDeclarationSyntax)
            .Where(m => m.Modifiers.Any(mod => mod.Text == "public"))
            .Select(m => m switch
            {
                MethodDeclarationSyntax method => (SyntaxToken?)method.Identifier,
                PropertyDeclarationSyntax property => property.Identifier,
                _ => null
            })
            .Where(identifier => identifier is not null)
            .Select(identifier => identifier!.Value);

        foreach (var identifier in publicMembers)
        {
            var restrictedKeywords = _map.RestrictedKeywordsFor(typeName, identifier.Text);

            foreach (var keyword in restrictedKeywords)
            {
                violations.Add(new Violation(
                    tree.FilePath,
                    identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    $"'{typeName}.{identifier.Text}' uses restricted vocabulary '{keyword}'. That concern was moved out of '{typeName}' - it should not reappear here."));
            }
        }
    }
}
