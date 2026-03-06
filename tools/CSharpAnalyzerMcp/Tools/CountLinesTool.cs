using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class CountLinesTool
{
    public static string CountLines(
        WorkspaceService workspace,
        string? filePath = null,
        string? namespaceName = null,
        bool showFiles = false,
        int maxFiles = 50)
    {
        var trees = workspace.GetAllSyntaxTrees().Select(t => t.Tree);

        if (!string.IsNullOrEmpty(filePath))
        {
            trees = trees.Where(t =>
                t.FilePath.Contains(filePath, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(namespaceName))
        {
            trees = trees.Where(t =>
            {
                var root = t.GetRoot();
                return root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax>()
                    .Any(ns =>
                    {
                        var nsName = ns.Name.ToString();
                        return nsName == namespaceName || nsName.StartsWith(namespaceName + ".");
                    });
            });
        }

        var treeList = trees.ToList();

        if (treeList.Count == 0)
        {
            return filePath != null
                ? $"No files found matching '{filePath}'"
                : namespaceName != null
                    ? $"No files found in namespace '{namespaceName}'"
                    : "No source files found in the project";
        }

        long totalLines = 0;
        long codeLines = 0;
        long blankLines = 0;
        long commentLines = 0;
        var fileStats = new List<object>();

        foreach (var tree in treeList)
        {
            var text = tree.GetText();
            var fileTotal = text.Lines.Count;
            var fileBlank = 0;
            var fileComment = 0;

            var commentLineNumbers = new HashSet<int>();
            var root = tree.GetRoot();
            foreach (var trivia in root.DescendantTrivia())
            {
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    var span = trivia.GetLocation().GetLineSpan();
                    for (var i = span.StartLinePosition.Line; i <= span.EndLinePosition.Line; i++)
                        commentLineNumbers.Add(i);
                }
            }

            foreach (var line in text.Lines)
            {
                var lineText = line.ToString().Trim();
                if (string.IsNullOrEmpty(lineText))
                {
                    fileBlank++;
                }
                else if (commentLineNumbers.Contains(line.LineNumber))
                {
                    fileComment++;
                }
            }

            var fileCode = fileTotal - fileBlank - fileComment;
            totalLines += fileTotal;
            codeLines += fileCode;
            blankLines += fileBlank;
            commentLines += fileComment;

            if (showFiles)
            {
                fileStats.Add(new
                {
                    file = tree.FilePath,
                    total = fileTotal,
                    code = fileCode,
                    blank = fileBlank,
                    comment = fileComment
                });
            }
        }

        if (showFiles)
        {
            fileStats = fileStats
                .OrderByDescending(f => ((dynamic)f).code)
                .Take(maxFiles)
                .ToList();
        }

        return ToonFormat.Toon.Encode(new
        {
            fileCount = treeList.Count,
            totalLines,
            codeLines,
            blankLines,
            commentLines,
            files = showFiles && fileStats.Count > 0 ? fileStats : null
        });
    }

    [McpServerTool(Name = "count_lines"),
     Description("Count lines of code in the project or a specific file/namespace. " +
                 "Returns total lines, code lines (non-empty, non-comment), and blank lines. " +
                 "Example: count_lines(filePath='CleaningSquadService.cs') for a specific file.")]
    public static async Task<string> McpCountLines(
        WorkspaceService workspace,
        [Description("Filter to a specific file (partial path match). " +
                     "Example: 'CleaningSquadService.cs' or 'Game/Leaderboard'")]
        string? filePath = null,
        [Description("Filter to a specific namespace. Example: 'Game.CleaningSquad'")]
        string? namespaceName = null,
        [Description("If true, show per-file breakdown. Default: false")]
        bool showFiles = false,
        [Description("Maximum number of files in breakdown. Default: 50")]
        int maxFiles = 50)
    {
        await workspace.EnsureLoadedAsync();
        return CountLines(workspace, filePath, namespaceName, showFiles, maxFiles);
    }
}
