using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cupid.Harness.Rules;

// HA10 - Entities must not take services in their constructor: an entity (a type implementing
// IDentifiable, see HA9) models data and identity; it must stay constructible from plain values
// and other entities/value objects. Accepting a service - a type living under
// OmniProduct_CoreDomain.Services.* or simply named *Service - in the constructor means the
// entity is reaching out to orchestration/behaviour that belongs in the service layer, inverting
// the dependency the workshop is teaching (services depend on entities, never the other way round).
public sealed class NoServicesInEntityConstructorRule : IHarnessRule
{
    private const string ServiceNamespacePrefix = "OmniProduct_CoreDomain.Services";
    private const string ServiceNameSuffix = "Service";
    private const string EntityMarkerInterfaceName = "IDentifiable";

    public string Id => "HA10";
    public string Name => "Entities must not take services in their constructor";

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
                var typeSymbol = model.GetDeclaredSymbol(typeDecl) as ITypeSymbol;
                if (typeSymbol == null || !IsEntity(typeSymbol))
                    continue;

                foreach (var parameter in GetConstructorParameters(typeDecl))
                {
                    var parameterSymbol = model.GetDeclaredSymbol(parameter) as IParameterSymbol;
                    var parameterType = parameterSymbol?.Type;

                    if (parameterType != null && IsServiceType(parameterType))
                    {
                        violations.Add(new Violation(
                            tree.FilePath,
                            parameter.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            $"'{typeSymbol.Name}' is an entity ({EntityMarkerInterfaceName}) but its constructor takes '{parameterType.Name}', which looks like a service. Entities must be constructible from plain values, value objects, or other entities - move the orchestration into the service layer instead."));
                    }
                }
            }
        }

        return violations;
    }

    private static IEnumerable<ParameterSyntax> GetConstructorParameters(TypeDeclarationSyntax typeDecl)
    {
        foreach (var ctor in typeDecl.Members.OfType<ConstructorDeclarationSyntax>())
        {
            foreach (var parameter in ctor.ParameterList.Parameters)
                yield return parameter;
        }

        if (typeDecl is RecordDeclarationSyntax { ParameterList: not null } record)
        {
            foreach (var parameter in record.ParameterList.Parameters)
                yield return parameter;
        }
    }

    private static bool IsEntity(ITypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i => i.Name == EntityMarkerInterfaceName);
    }

    private static bool IsServiceType(ITypeSymbol typeSymbol)
    {
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        var isInServiceNamespace = ns != null
                                    && (ns == ServiceNamespacePrefix || ns.StartsWith(ServiceNamespacePrefix + ".", StringComparison.Ordinal));

        return isInServiceNamespace || typeSymbol.Name.EndsWith(ServiceNameSuffix, StringComparison.Ordinal);
    }
}
