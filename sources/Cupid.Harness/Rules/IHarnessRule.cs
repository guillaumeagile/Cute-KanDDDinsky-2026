using Microsoft.CodeAnalysis;

namespace Cupid.Harness.Rules;

public enum HarnessSeverity
{
    Error,
    Warning
}

public interface IHarnessRule
{
    string Id { get; }
    string Name { get; }

    // Warning rules are reported as [WARN] and never fail the harness.
    HarnessSeverity Severity => HarnessSeverity.Error;

    IReadOnlyList<Violation> Check(IReadOnlyList<SyntaxTree> trees, Compilation compilation);
}

public sealed record Violation(string FilePath, int Line, string Message);
