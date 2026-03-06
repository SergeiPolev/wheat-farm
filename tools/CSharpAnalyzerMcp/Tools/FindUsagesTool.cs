using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class FindUsagesTool
{
    public static async Task<string> FindUsages(
        WorkspaceService workspace,
        string symbolName,
        string symbolKind = "all",
        string? containingType = null,
        int maxResults = 50)
    {
        var symbols = workspace.FindSymbolsByName(symbolName, symbolKind, containingType);

        if (symbols.Count == 0)
            return $"Symbol '{symbolName}' not found. Try search_symbols to find it.";

        if (symbols.Count > 10)
        {
            var options = symbols.Take(20).Select(s => new
            {
                name = s.Name,
                kind = s.Kind.ToString().ToLowerInvariant(),
                containingType = s.ContainingType?.ToDisplayString() ?? "",
                display = s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            }).ToList();

            return ToonFormat.Toon.Encode(new
            {
                error = "too_many_matches",
                message = $"Found {symbols.Count} symbols matching '{symbolName}'. Use containingType or symbolKind to narrow down:",
                matches = options
            });
        }

        var solution = workspace.Project.Solution;
        var allUsages = new List<object>();
        var symbolInfos = new List<object>();

        foreach (var symbol in symbols)
        {
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution);

            foreach (var reference in references)
            {
                var (defPath, defLine) = workspace.GetSymbolLocation(reference.Definition);
                symbolInfos.Add(new
                {
                    name = reference.Definition.Name,
                    kind = reference.Definition.Kind.ToString().ToLowerInvariant(),
                    containingType = reference.Definition.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    display = reference.Definition.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    definitionFile = defPath,
                    definitionLine = defLine
                });

                foreach (var location in reference.Locations)
                {
                    if (allUsages.Count >= maxResults) break;

                    var lineSpan = location.Location.GetLineSpan();
                    var filePath = lineSpan.Path;
                    var line = lineSpan.StartLinePosition.Line + 1;

                    string? containingMember = null;
                    var syntaxNode = location.Location.SourceTree?.GetRoot().FindNode(location.Location.SourceSpan);
                    if (syntaxNode != null)
                    {
                        var memberDecl = syntaxNode.Ancestors()
                            .FirstOrDefault(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax);
                        if (memberDecl != null)
                        {
                            var semanticModel = workspace.GetCompilationForTree(location.Location.SourceTree!).GetSemanticModel(location.Location.SourceTree!);
                            var memberSymbol = semanticModel.GetDeclaredSymbol(memberDecl);
                            if (memberSymbol != null)
                            {
                                containingMember = memberSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                            }
                        }
                    }

                    var lineText = workspace.GetLineText(filePath, line);

                    allUsages.Add(new
                    {
                        filePath,
                        line,
                        containingMember,
                        lineText
                    });
                }
            }
        }

        if (allUsages.Count == 0)
            return $"No usages found for '{symbolName}'";

        return ToonFormat.Toon.Encode(new
        {
            symbols = symbolInfos,
            totalUsages = allUsages.Count,
            usages = allUsages
        });
    }

    [McpServerTool(Name = "find_usages"),
     Description("Find all locations where a symbol (class, method, property, field) is referenced in code. " +
                 "Useful for understanding dependencies, impact analysis, and refactoring. " +
                 "Example: find_usages('CleaningSquadService') shows everywhere it's used.")]
    public static async Task<string> McpFindUsages(
        WorkspaceService workspace,
        [Description("Name of the symbol to find usages of. " +
                     "Examples: 'CleaningSquadService', 'Initialize', 'IsActive'")]
        string symbolName,
        [Description("Filter by symbol kind: 'class', 'method', 'property', 'field', 'event', or 'all'. Default: 'all'")]
        string symbolKind = "all",
        [Description("For members (method/property/field), specify the containing type to disambiguate. " +
                     "Example: containingType='CleaningSquadService' with symbolName='Initialize'")]
        string? containingType = null,
        [Description("Maximum number of usage locations to return. Default: 50")]
        int maxResults = 50)
    {
        await workspace.EnsureLoadedAsync();
        return await FindUsages(workspace, symbolName, symbolKind, containingType, maxResults);
    }
}
