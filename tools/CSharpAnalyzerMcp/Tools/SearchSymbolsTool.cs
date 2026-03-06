using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class SearchSymbolsTool
{
    [McpServerTool(Name = "search_symbols"),
     Description("Search for types by name pattern. Supports wildcards (* and ?) and substring matching. " +
                 "Returns type name, kind, namespace, base type, interfaces, and file location.")]
    public static async Task<string> McpSearch(
        WorkspaceService workspace,
        [Description("Search pattern. Examples: '*Service', 'Cleaning*', 'IFeatureInstaller'. " +
                     "Use * for wildcard, or plain text for substring match.")]
        string query,
        [Description("Filter by symbol kind: 'class', 'interface', 'enum', 'struct', 'delegate', or 'all'. Default: 'all'")]
        string kind = "all",
        [Description("Maximum number of results to return. Default: 50")]
        int maxResults = 50)
    {
        await workspace.EnsureLoadedAsync();
        return Search(workspace, query, kind, maxResults);
    }

    public static string Search(
        WorkspaceService workspace,
        string query,
        string kind = "all",
        int maxResults = 50)
    {
        var types = workspace.SearchTypes(query);

        if (kind != "all")
        {
            types = types.Where(t => MatchesKind(t, kind)).ToList();
        }

        if (types.Count == 0)
            return $"No types found matching '{query}'";

        var results = types
            .Take(maxResults)
            .Select(t =>
            {
                var (path, line) = workspace.GetSymbolLocation(t);
                var baseType = t.BaseType != null && t.BaseType.SpecialType != SpecialType.System_Object
                    ? t.BaseType.Name
                    : null;
                var interfaces = t.Interfaces.Select(i => i.Name).ToArray();

                return new
                {
                    name = t.Name,
                    fullName = t.ToDisplayString(),
                    kind = t.TypeKind.ToString().ToLowerInvariant(),
                    @namespace = t.ContainingNamespace?.ToDisplayString() ?? "",
                    baseType,
                    interfaces = interfaces.Length > 0 ? interfaces : null,
                    filePath = path,
                    line
                };
            })
            .ToList();

        return ToonFormat.Toon.Encode(new
        {
            totalMatches = types.Count,
            showing = results.Count,
            results
        });
    }

    private static bool MatchesKind(INamedTypeSymbol type, string kind) => kind.ToLowerInvariant() switch
    {
        "class" => type.TypeKind == TypeKind.Class,
        "interface" => type.TypeKind == TypeKind.Interface,
        "enum" => type.TypeKind == TypeKind.Enum,
        "struct" => type.TypeKind == TypeKind.Struct,
        "delegate" => type.TypeKind == TypeKind.Delegate,
        _ => true
    };
}
