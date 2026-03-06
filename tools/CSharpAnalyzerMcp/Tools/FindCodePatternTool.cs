using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using CSharpAnalyzerMcp.Tools.Patterns;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class FindCodePatternTool
{
    private static readonly List<IPatternDetector> Detectors =
    [
        new UncheckedNullableDetector(),
        new FireAndForgetAsyncDetector(),
        new SubscribeWithoutDisposeDetector(),
        new ViewCallsServiceDetector()
    ];

    public static string FindCodePattern(
        WorkspaceService workspace,
        string pattern,
        string? scope = null,
        int maxResults = 30,
        string severity = "all")
    {
        if (pattern == "list")
        {
            var groups = Detectors
                .GroupBy(d => d.Group)
                .Select(g => new
                {
                    group = g.Key,
                    patterns = g.Select(d => new
                    {
                        name = d.Name,
                        description = d.Description,
                        defaultSeverity = d.DefaultSeverity
                    }).ToList()
                }).ToList();

            return ToonFormat.Toon.Encode(new { availablePatterns = groups });
        }

        var detector = Detectors.FirstOrDefault(d => d.Name == pattern);
        if (detector == null)
        {
            var available = string.Join(", ", Detectors.Select(d => d.Name));
            return $"Unknown pattern: '{pattern}'. Available patterns: {available}. Use pattern='list' to see descriptions.";
        }

        var detectLimit = severity != "all" ? maxResults * 3 : maxResults;
        var matches = detector.Detect(workspace, scope, detectLimit);

        if (severity != "all")
        {
            matches = matches.Where(m => m.Severity == severity).Take(maxResults).ToList();
        }

        return ToonFormat.Toon.Encode(new
        {
            pattern = detector.Name,
            description = detector.Description,
            group = detector.Group,
            scope = scope ?? "<entire project>",
            matchCount = matches.Count,
            matches = matches.Select(m => new
            {
                filePath = m.FilePath,
                line = m.Line,
                containingMember = m.ContainingMember,
                code = m.Code,
                detail = m.Detail,
                severity = m.Severity
            }).ToList()
        });
    }

    [McpServerTool(Name = "find_code_pattern"),
     Description("Find semantic code patterns and anti-patterns using Roslyn analysis. " +
                 "Unlike text grep, understands types, nullability, and project conventions. " +
                 "Use pattern='list' to see all available detectors. " +
                 "Example: find_code_pattern(pattern='unchecked_nullable', scope='Game.Features.Pricing')")]
    public static async Task<string> McpFindCodePattern(
        WorkspaceService workspace,
        [Description("Detector name or 'list' for available patterns. " +
                     "Available: 'unchecked_nullable', 'fire_and_forget_async', 'subscribe_without_dispose', 'view_calls_service'")]
        string pattern,
        [Description("Namespace or file path to narrow search. Without it — entire project.")]
        string? scope = null,
        [Description("Maximum results. Default: 30")]
        int maxResults = 30,
        [Description("Filter by severity: 'error', 'warning', 'info', or 'all'. Default: 'all'")]
        string severity = "all")
    {
        await workspace.EnsureLoadedAsync();
        return FindCodePattern(workspace, pattern, scope, maxResults, severity);
    }
}
