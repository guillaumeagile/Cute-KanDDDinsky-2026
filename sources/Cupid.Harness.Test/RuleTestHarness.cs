using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cupid.Harness.Test;

// Compiles a small in-memory source snippet so each rule can be exercised against sample code
// instead of the real (and constantly moving) OmniProduct-CoreDomain sources.
internal static class RuleTestHarness
{
    public static (IReadOnlyList<SyntaxTree> Trees, Compilation Compilation) Compile(string source, string filePath = "Sample.cs")
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: filePath);

        var compilation = CSharpCompilation.Create(
            "Cupid.Harness.Test.Sample",
            new[] { tree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(OneOf.OneOf<,>).Assembly.Location)
            },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (new[] { tree }, compilation);
    }
}
