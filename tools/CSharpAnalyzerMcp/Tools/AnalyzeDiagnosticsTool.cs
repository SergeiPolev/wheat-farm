using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class AnalyzeDiagnosticsTool
{
    public static string AnalyzeDiagnostics(
        WorkspaceService workspace,
        string? filePath = null,
        string severity = "all",
        string? diagnosticId = null,
        int maxResults = 50)
    {
        var diagnostics = workspace.GetAllDiagnostics();

        IEnumerable<Diagnostic> filtered = diagnostics;

        if (severity != "all")
        {
            var severityFilter = severity.ToLowerInvariant() switch
            {
                "error" => DiagnosticSeverity.Error,
                "warning" => DiagnosticSeverity.Warning,
                "info" => DiagnosticSeverity.Info,
                _ => (DiagnosticSeverity?)null
            };
            if (severityFilter != null)
                filtered = filtered.Where(d => d.Severity == severityFilter);
        }

        filtered = filtered.Where(d => d.Severity != DiagnosticSeverity.Hidden);

        if (!string.IsNullOrEmpty(filePath))
        {
            filtered = filtered.Where(d =>
            {
                var path = d.Location.GetLineSpan().Path;
                return path != null && path.Contains(filePath, StringComparison.OrdinalIgnoreCase);
            });
        }

        if (!string.IsNullOrEmpty(diagnosticId))
        {
            filtered = filtered.Where(d =>
                d.Id.StartsWith(diagnosticId, StringComparison.OrdinalIgnoreCase));
        }

        var allFiltered = filtered.ToList();

        var summary = new
        {
            errors = allFiltered.Count(d => d.Severity == DiagnosticSeverity.Error),
            warnings = allFiltered.Count(d => d.Severity == DiagnosticSeverity.Warning),
            info = allFiltered.Count(d => d.Severity == DiagnosticSeverity.Info)
        };

        var byId = allFiltered
            .GroupBy(d => d.Id)
            .Select(g => new
            {
                id = g.Key,
                count = g.Count(),
                severity = g.First().Severity.ToString().ToLowerInvariant(),
                description = g.First().Descriptor.Title.ToString()
            })
            .OrderByDescending(g => g.count)
            .ToList();

        var results = allFiltered
            .Take(maxResults)
            .Select(d =>
            {
                var lineSpan = d.Location.GetLineSpan();
                return new
                {
                    id = d.Id,
                    severity = d.Severity.ToString().ToLowerInvariant(),
                    message = d.GetMessage(),
                    filePath = lineSpan.Path,
                    line = lineSpan.StartLinePosition.Line + 1,
                    column = lineSpan.StartLinePosition.Character + 1
                };
            })
            .ToList();

        return ToonFormat.Toon.Encode(new
        {
            summary,
            totalDiagnostics = allFiltered.Count,
            showing = results.Count,
            byDiagnosticId = byId.Count > 0 ? byId : null,
            diagnostics = results
        });
    }

    [McpServerTool(Name = "analyze_diagnostics"),
     Description("Get compiler warnings, errors, and Roslyn diagnostics from the project. " +
                 "Can filter by file path, severity, or diagnostic ID. " +
                 "Example: analyze_diagnostics(severity='error') shows all compilation errors.")]
    public static async Task<string> McpAnalyzeDiagnostics(
        WorkspaceService workspace,
        [Description("Filter to diagnostics in this file (partial path match). " +
                     "Example: 'CleaningSquadService.cs' or 'Game/Leaderboard'")]
        string? filePath = null,
        [Description("Filter by severity: 'error', 'warning', 'info', or 'all'. Default: 'all'")]
        string severity = "all",
        [Description("Filter by diagnostic ID prefix. Example: 'CS0' for all CS0xxx warnings, 'CS8' for nullable warnings")]
        string? diagnosticId = null,
        [Description("Maximum number of diagnostics to return. Default: 50")]
        int maxResults = 50)
    {
        await workspace.EnsureLoadedAsync();
        return AnalyzeDiagnostics(workspace, filePath, severity, diagnosticId, maxResults);
    }
}
