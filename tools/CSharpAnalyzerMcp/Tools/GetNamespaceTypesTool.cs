using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetNamespaceTypesTool
{
    public static string GetNamespaceTypes(
        WorkspaceService workspace,
        string namespaceName,
        bool includeNested = false,
        int maxResults = 100)
    {
        var allTypes = workspace.GetAllSourceTypes()
            .Where(t =>
            {
                var ns = t.ContainingNamespace?.ToDisplayString() ?? "";
                if (includeNested)
                    return ns == namespaceName || ns.StartsWith(namespaceName + ".");
                return ns == namespaceName;
            })
            .Where(t => t.ContainingType == null)
            .OrderBy(t => t.Name)
            .ToList();

        if (allTypes.Count == 0)
        {
            var matchingNamespaces = workspace.GetAllSourceTypes()
                .Select(t => t.ContainingNamespace?.ToDisplayString() ?? "")
                .Where(ns => ns.Contains(namespaceName, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .Take(10)
                .ToList();

            if (matchingNamespaces.Count > 0)
            {
                return ToonFormat.Toon.Encode(new
                {
                    error = "not_found",
                    message = $"No types found in namespace '{namespaceName}'. Did you mean one of these?",
                    suggestions = matchingNamespaces
                });
            }

            return $"No types found in namespace '{namespaceName}'";
        }

        var results = allTypes
            .Take(maxResults)
            .Select(t =>
            {
                var (path, line) = workspace.GetSymbolLocation(t);
                var baseType = t.BaseType != null && t.BaseType.SpecialType != SpecialType.System_Object
                    ? t.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                    : null;

                return new
                {
                    name = t.Name,
                    fullName = t.ToDisplayString(),
                    kind = t.TypeKind.ToString().ToLowerInvariant(),
                    accessibility = t.DeclaredAccessibility.ToString().ToLowerInvariant(),
                    isAbstract = t.IsAbstract,
                    isStatic = t.IsStatic,
                    baseType,
                    interfaces = t.Interfaces
                        .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                        .ToArray(),
                    nestedTypeCount = t.GetTypeMembers().Length,
                    memberCount = t.GetMembers().Where(m => !m.IsImplicitlyDeclared).Count(),
                    filePath = path,
                    line
                };
            })
            .ToList();

        var subNamespaces = new HashSet<string>();
        foreach (var (tree, _) in workspace.GetAllSyntaxTrees())
        {
            var treeRoot = tree.GetRoot();
            foreach (var nsDecl in treeRoot.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax>())
            {
                var nsName = nsDecl.Name.ToString();
                if (nsName.StartsWith(namespaceName + ".") && nsName != namespaceName)
                {
                    var suffix = nsName[(namespaceName.Length + 1)..];
                    var dotIndex = suffix.IndexOf('.');
                    subNamespaces.Add(dotIndex >= 0 ? namespaceName + "." + suffix[..dotIndex] : nsName);
                }
            }
        }
        var subNamespaceList = subNamespaces.OrderBy(ns => ns).ToList();

        return ToonFormat.Toon.Encode(new
        {
            @namespace = namespaceName,
            totalTypes = allTypes.Count,
            showing = results.Count,
            subNamespaces = subNamespaceList.Count > 0 ? subNamespaceList : null,
            types = results
        });
    }

    [McpServerTool(Name = "get_namespace_types"),
     Description("List all types defined in a namespace. Useful for exploring a feature's code structure. " +
                 "Example: get_namespace_types('Game.CleaningSquad') shows all types in the CleaningSquad feature.")]
    public static async Task<string> McpGetNamespaceTypes(
        WorkspaceService workspace,
        [Description("Namespace to list types from. Examples: 'Game.CleaningSquad', 'Game.Hotel', 'ReduxTools.Feature'")]
        string namespaceName,
        [Description("If true, include types from sub-namespaces (e.g., 'Game.CleaningSquad.Ui'). Default: false")]
        bool includeNested = false,
        [Description("Maximum number of types to return. Default: 100")]
        int maxResults = 100)
    {
        await workspace.EnsureLoadedAsync();
        return GetNamespaceTypes(workspace, namespaceName, includeNested, maxResults);
    }
}
