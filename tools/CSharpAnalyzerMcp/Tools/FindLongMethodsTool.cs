using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class FindLongMethodsTool
{
    public static string FindLongMethods(
        WorkspaceService workspace,
        string? namespaceName = null,
        string? filePath = null,
        int minLines = 50,
        int maxResults = 30,
        string sortBy = "lines")
    {
        var results = new List<MethodInfo>();

        foreach (var (tree, compilation) in workspace.GetAllSyntaxTrees())
        {
            if (!string.IsNullOrEmpty(filePath) &&
                !tree.FilePath.Contains(filePath, StringComparison.OrdinalIgnoreCase))
                continue;

            var root = tree.GetRoot();

            if (!string.IsNullOrEmpty(namespaceName))
            {
                var hasNamespace = root.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Any(ns =>
                    {
                        var nsName = ns.Name.ToString();
                        return nsName == namespaceName || nsName.StartsWith(namespaceName + ".");
                    });
                if (!hasNamespace) continue;
            }

            var semanticModel = compilation.GetSemanticModel(tree);

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var span = method.GetLocation().GetLineSpan();
                var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

                if (lines < minLines) continue;

                var containingType = method.Ancestors()
                    .OfType<TypeDeclarationSyntax>()
                    .FirstOrDefault();

                var typeSymbol = containingType != null
                    ? semanticModel.GetDeclaredSymbol(containingType)
                    : null;

                results.Add(new MethodInfo
                {
                    Name = method.Identifier.Text,
                    ContainingType = typeSymbol?.Name ?? "<unknown>",
                    Namespace = typeSymbol?.ContainingNamespace?.ToDisplayString() ?? "",
                    Lines = lines,
                    Parameters = method.ParameterList.Parameters.Count,
                    Complexity = CalculateCyclomaticComplexity(method, semanticModel),
                    FilePath = tree.FilePath,
                    StartLine = span.StartLinePosition.Line + 1,
                    EndLine = span.EndLinePosition.Line + 1
                });
            }

            foreach (var ctor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
            {
                var span = ctor.GetLocation().GetLineSpan();
                var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

                if (lines < minLines) continue;

                var containingType = ctor.Ancestors()
                    .OfType<TypeDeclarationSyntax>()
                    .FirstOrDefault();

                var typeSymbol = containingType != null
                    ? semanticModel.GetDeclaredSymbol(containingType)
                    : null;

                results.Add(new MethodInfo
                {
                    Name = ".ctor",
                    ContainingType = typeSymbol?.Name ?? "<unknown>",
                    Namespace = typeSymbol?.ContainingNamespace?.ToDisplayString() ?? "",
                    Lines = lines,
                    Parameters = ctor.ParameterList.Parameters.Count,
                    Complexity = CalculateCyclomaticComplexity(ctor, semanticModel),
                    FilePath = tree.FilePath,
                    StartLine = span.StartLinePosition.Line + 1,
                    EndLine = span.EndLinePosition.Line + 1
                });
            }
        }

        var sorted = sortBy switch
        {
            "complexity" => results.OrderByDescending(m => m.Complexity).ThenByDescending(m => m.Lines),
            "name" => results.OrderBy(m => m.ContainingType).ThenBy(m => m.Name),
            _ => results.OrderByDescending(m => m.Lines)
        };

        var output = sorted.Take(maxResults).Select(m => new
        {
            method = $"{m.ContainingType}.{m.Name}",
            lines = m.Lines,
            complexity = m.Complexity,
            parameters = m.Parameters,
            @namespace = m.Namespace,
            filePath = m.FilePath,
            startLine = m.StartLine,
            endLine = m.EndLine
        }).ToList();

        return ToonFormat.Toon.Encode(new
        {
            filter = new
            {
                namespaceName,
                filePath,
                minLines
            },
            totalFound = results.Count,
            showing = output.Count,
            averageLines = results.Count > 0 ? Math.Round(results.Average(m => m.Lines), 1) : 0,
            maxLines = results.Count > 0 ? results.Max(m => m.Lines) : 0,
            methods = output
        });
    }

    [McpServerTool(Name = "find_long_methods"),
     Description("Find methods longer than a threshold in a namespace or the entire project. " +
                 "Useful for enforcing the 200-300 line file limit and finding methods that need refactoring. " +
                 "Example: find_long_methods(namespaceName='Game.CleaningSquad', minLines=30)")]
    public static async Task<string> McpFindLongMethods(
        WorkspaceService workspace,
        [Description("Filter to a specific namespace. If empty, scans entire project.")]
        string? namespaceName = null,
        [Description("Filter to a specific file (partial path match).")]
        string? filePath = null,
        [Description("Minimum number of lines to report. Default: 50")]
        int minLines = 50,
        [Description("Maximum number of results. Default: 30")]
        int maxResults = 30,
        [Description("Sort by: 'lines' (longest first), 'complexity', 'name'. Default: 'lines'")]
        string sortBy = "lines")
    {
        await workspace.EnsureLoadedAsync();
        return FindLongMethods(workspace, namespaceName, filePath, minLines, maxResults, sortBy);
    }

    private class MethodInfo
    {
        public string Name { get; set; } = "";
        public string ContainingType { get; set; } = "";
        public string Namespace { get; set; } = "";
        public int Lines { get; set; }
        public int Parameters { get; set; }
        public int Complexity { get; set; }
        public string FilePath { get; set; } = "";
        public int StartLine { get; set; }
        public int EndLine { get; set; }
    }
}
