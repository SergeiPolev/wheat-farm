using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetTypeHierarchyTool
{
    public static async Task<string> GetTypeHierarchy(
        WorkspaceService workspace,
        string className,
        string direction = "both",
        int maxDepth = 10,
        int maxResults = 100)
    {
        var types = workspace.FindTypesByName(className);

        if (types.Count == 0)
            return $"Type '{className}' not found. Try search_symbols to find it.";

        if (types.Count > 1)
        {
            var options = types.Select(t => t.ToDisplayString()).ToList();
            return ToonFormat.Toon.Encode(new
            {
                error = "ambiguous",
                message = $"Found {types.Count} types matching '{className}'. Please use fully-qualified name:",
                matches = options
            });
        }

        var targetType = types[0];
        var (targetPath, targetLine) = workspace.GetSymbolLocation(targetType);

        List<object>? ancestors = null;
        if (direction is "up" or "both")
        {
            ancestors = [];
            var current = targetType.BaseType;
            var ancestorDepth = 0;
            while (current != null && current.SpecialType != SpecialType.System_Object && ancestorDepth < maxDepth)
            {
                var (path, line) = workspace.GetSymbolLocation(current);
                ancestors.Add(new
                {
                    name = current.Name,
                    fullName = current.ToDisplayString(),
                    kind = current.TypeKind.ToString().ToLowerInvariant(),
                    isAbstract = current.IsAbstract,
                    interfaces = current.Interfaces
                        .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                        .ToArray(),
                    filePath = path,
                    line,
                    isExternal = path == null
                });
                current = current.BaseType;
                ancestorDepth++;
            }

            if (targetType.TypeKind != TypeKind.Interface)
            {
                var implementedInterfaces = targetType.AllInterfaces
                    .Select(i =>
                    {
                        var (path, line) = workspace.GetSymbolLocation(i);
                        return new
                        {
                            name = i.Name,
                            fullName = i.ToDisplayString(),
                            kind = "interface",
                            isAbstract = true,
                            interfaces = Array.Empty<string>(),
                            filePath = path,
                            line,
                            isExternal = path == null
                        };
                    })
                    .Cast<object>()
                    .ToList();

                if (implementedInterfaces.Count > 0)
                {
                    ancestors.AddRange(implementedInterfaces);
                }
            }
        }

        List<object>? descendants = null;
        if (direction is "down" or "both")
        {
            var solution = workspace.Project.Solution;

            if (targetType.TypeKind == TypeKind.Interface)
            {
                var found = await SymbolFinder.FindImplementationsAsync(targetType, solution);
                descendants = found.OfType<INamedTypeSymbol>()
                    .Take(maxResults)
                    .Select(t =>
                    {
                        var (path, line) = workspace.GetSymbolLocation(t);
                        return (object)new
                        {
                            name = t.Name,
                            fullName = t.ToDisplayString(),
                            kind = t.TypeKind.ToString().ToLowerInvariant(),
                            isAbstract = t.IsAbstract,
                            baseType = t.BaseType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            filePath = path,
                            line
                        };
                    })
                    .OrderBy(d => ((dynamic)d).fullName)
                    .ToList();
            }
            else
            {
                var found = await SymbolFinder.FindDerivedClassesAsync(targetType, solution);
                descendants = found
                    .Take(maxResults)
                    .Select(t =>
                    {
                        var (path, line) = workspace.GetSymbolLocation(t);
                        return (object)new
                        {
                            name = t.Name,
                            fullName = t.ToDisplayString(),
                            kind = t.TypeKind.ToString().ToLowerInvariant(),
                            isAbstract = t.IsAbstract,
                            baseType = t.BaseType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            filePath = path,
                            line
                        };
                    })
                    .OrderBy(d => ((dynamic)d).fullName)
                    .ToList();
            }
        }

        return ToonFormat.Toon.Encode(new
        {
            target = new
            {
                name = targetType.Name,
                fullName = targetType.ToDisplayString(),
                kind = targetType.TypeKind.ToString().ToLowerInvariant(),
                isAbstract = targetType.IsAbstract,
                filePath = targetPath,
                line = targetLine
            },
            ancestors = ancestors?.Count > 0 ? ancestors : null,
            ancestorCount = ancestors?.Count ?? 0,
            descendants = descendants?.Count > 0 ? descendants : null,
            descendantCount = descendants?.Count ?? 0
        });
    }

    [McpServerTool(Name = "get_type_hierarchy"),
     Description("Get the inheritance hierarchy of a type — base classes going up and/or derived classes going down. " +
                 "Useful for understanding class relationships, finding all variants of a base class, etc. " +
                 "Example: get_type_hierarchy('MonoBehaviour', direction='down') finds all MonoBehaviours.")]
    public static async Task<string> McpGetTypeHierarchy(
        WorkspaceService workspace,
        [Description("Class or interface name. Can be short name or fully-qualified.")]
        string className,
        [Description("Direction to traverse: 'up' (base types only), 'down' (descendants only), 'both'. Default: 'both'")]
        string direction = "both",
        [Description("Maximum depth to traverse down the hierarchy. Default: 10")]
        int maxDepth = 10,
        [Description("Maximum number of descendants to return. Default: 100")]
        int maxResults = 100)
    {
        await workspace.EnsureLoadedAsync();
        return await GetTypeHierarchy(workspace, className, direction, maxDepth, maxResults);
    }
}
