using CSharpAnalyzerMcp.Services;

namespace CSharpAnalyzerMcp.Tools.Patterns;

public interface IPatternDetector
{
    string Name { get; }
    string Description { get; }
    string Group { get; }
    string DefaultSeverity { get; }

    List<PatternMatch> Detect(WorkspaceService workspace, string? scope, int maxResults);
}

public record PatternMatch(
    string FilePath,
    int Line,
    string? ContainingMember,
    string Code,
    string Detail,
    string Severity);
