using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class FindImplementationsTool
{
    [McpServerTool(Name = "find_implementations"),
     Description("Find all classes that implement an interface or inherit from a base class. " +
                 "Useful for understanding polymorphism, finding all handlers, services, etc. " +
                 "Example: find_implementations('IFeatureInstaller') returns all feature installers.")]
    public static async Task<string> McpFindImplementations(
        WorkspaceService workspace,
        [Description("Interface or class name. Can be short name or fully-qualified. " +
                     "Examples: 'IFeatureInstaller', 'IDisposable', 'MonoBehaviour'")]
        string interfaceOrClassName,
        [Description("If true, include indirect implementations (classes implementing derived interfaces). Default: true")]
        bool includeIndirect = true,
        [Description("Maximum number of results. Default: 100")]
        int maxResults = 100)
    {
        await workspace.EnsureLoadedAsync();
        return await FindImplementations(workspace, interfaceOrClassName, includeIndirect, maxResults);
    }

    public static async Task<string> FindImplementations(
        WorkspaceService workspace,
        string interfaceOrClassName,
        bool includeIndirect = true,
        int maxResults = 100)
    {
        var types = workspace.FindTypesByName(interfaceOrClassName);

        if (types.Count == 0)
            return $"Type '{interfaceOrClassName}' not found. Try search_symbols to find it.";

        if (types.Count > 1)
        {
            var options = types.Select(t => t.ToDisplayString()).ToList();
            return ToonFormat.Toon.Encode(new
            {
                error = "ambiguous",
                message = $"Found {types.Count} types matching '{interfaceOrClassName}'. Please use fully-qualified name:",
                matches = options
            });
        }

        var targetType = types[0];
        var solution = workspace.Project.Solution;

        IEnumerable<INamedTypeSymbol> implementations;

        if (targetType.TypeKind == TypeKind.Interface)
        {
            var found = await SymbolFinder.FindImplementationsAsync(targetType, solution, transitive: includeIndirect);
            implementations = found.OfType<INamedTypeSymbol>();
        }
        else
        {
            var found = await SymbolFinder.FindDerivedClassesAsync(targetType, solution, transitive: includeIndirect);
            implementations = found;
        }

        var results = implementations
            .Take(maxResults)
            .Select(t =>
            {
                var (path, line) = workspace.GetSymbolLocation(t);
                return new
                {
                    name = t.Name,
                    fullName = t.ToDisplayString(),
                    kind = t.TypeKind.ToString().ToLowerInvariant(),
                    isAbstract = t.IsAbstract,
                    baseType = t.BaseType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    directInterfaces = t.Interfaces
                        .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                        .ToArray(),
                    filePath = path,
                    line
                };
            })
            .OrderBy(r => r.fullName)
            .ToList();

        if (results.Count == 0)
            return $"No implementations found for '{targetType.ToDisplayString()}'";

        return ToonFormat.Toon.Encode(new
        {
            target = new
            {
                name = targetType.Name,
                fullName = targetType.ToDisplayString(),
                kind = targetType.TypeKind.ToString().ToLowerInvariant()
            },
            totalFound = results.Count,
            implementations = results
        });
    }
}
