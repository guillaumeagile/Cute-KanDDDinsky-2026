using Cupid.Harness.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Cupid.Harness [--step 1.1|1.2] <path-to-source-directory> [more-paths...]");
    return 2;
}

var step = "all";
var pathArgs = args.ToList();

var stepFlagIndex = pathArgs.FindIndex(a => a == "--step");
if (stepFlagIndex >= 0)
{
    if (stepFlagIndex + 1 >= pathArgs.Count)
    {
        Console.Error.WriteLine("--step requires a value: 1.1 or 1.2");
        return 2;
    }

    step = pathArgs[stepFlagIndex + 1];
    pathArgs.RemoveRange(stepFlagIndex, 2);
}

if (pathArgs.Count == 0)
{
    Console.Error.WriteLine("Usage: Cupid.Harness [--step 1.1|1.2] <path-to-source-directory> [more-paths...]");
    return 2;
}

var files = pathArgs
    .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
             && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
    .OrderBy(f => f, StringComparer.Ordinal) // deterministic ordering regardless of filesystem enumeration order
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine("No .cs files found under the given path(s).");
    return 2;
}

var trees = files
    .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f))
    .ToList();

// The domain project enables <ImplicitUsings>, so its files use System/Linq/... types without a `using`.
// Mirror those global usings in a compilation-only tree (not handed to the rules) so semantic checks can
// resolve symbols such as DateTime instead of silently skipping them.
var implicitUsings = CSharpSyntaxTree.ParseText(
    "global using System; global using System.Collections.Generic; global using System.IO; " +
    "global using System.Linq; global using System.Net.Http; global using System.Threading; " +
    "global using System.Threading.Tasks;");

var compilation = CSharpCompilation.Create(
    "CupidHarness.Analysis",
    trees.Append(implicitUsings),
    references: new[]
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(OneOf.OneOf<,>).Assembly.Location)
    },
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var languageMapPath = Path.Combine(AppContext.BaseDirectory, "ubiquitous-language-map.json");
var languageMap = new Lazy<UbiquitousLanguageMap>(() => UbiquitousLanguageMap.Load(languageMapPath));

var step1Dot1Rules = new IHarnessRule[]
{
    new NoGodClassRule(),
    new SingleConcernRule(languageMap.Value),
    new FanOutRule(),
    new EntityMustImplementIdentifiableRule(),
    new NoServicesInEntityConstructorRule(),
    new NoPersistenceInDomainRule(),
    new RichIdentifierRule(),
    new BareIdentifierIsWeakRule(),
};

var step1Dot2Rules = new IHarnessRule[]
{
    new ImmutableDataStructuresRule(),
    new RecordsAreValueObjectsRule(),
    new NoInheritanceRule(),
    new NoThrowInConstructorRule(),
    new ProductHasOneOfPropertyRule(),
    new ValueObjectFactoryRule(),
    new MatchOverOneOfRule(),
    new NoClockInModelRule(),
    new SellChecksCanSellBeforeWithdrawRule(),
};

IReadOnlyList<IHarnessRule> rules = step switch
{
    "1.1" => step1Dot1Rules,
    "1.2" => step1Dot2Rules,
    "all" => step1Dot1Rules.Concat(step1Dot2Rules).ToList(),
    _ => throw new ArgumentException($"Unknown --step value '{step}'. Use 1.1, 1.2, or omit for all.")
};

Console.WriteLine($"CUPID step {step} harness - deterministic structural checks");
Console.WriteLine($"Analyzed {files.Count} file(s) under: {string.Join(", ", pathArgs)}");
Console.WriteLine();

var overallPass = true;

foreach (var rule in rules)
{
    var violations = rule.Check(trees, compilation)
        .OrderBy(v => v.FilePath, StringComparer.Ordinal)
        .ThenBy(v => v.Line)
        .ToList();

    var isWarning = rule.Severity == HarnessSeverity.Warning;
    var status = violations.Count == 0 ? "PASS" : isWarning ? "WARN" : "FAIL";
    if (violations.Count > 0 && !isWarning)
        overallPass = false;

    Console.WriteLine($"[{status}] {rule.Id} - {rule.Name}");

    foreach (var violation in violations)
    {
        Console.WriteLine($"    {RelativePath(violation.FilePath)}:{violation.Line}  {violation.Message}");
    }
}

Console.WriteLine();
Console.WriteLine(overallPass ? "HARNESS RESULT: PASS" : "HARNESS RESULT: FAIL");

return overallPass ? 0 : 1;

static string RelativePath(string path)
{
    try
    {
        return Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
    }
    catch
    {
        return path;
    }
}
