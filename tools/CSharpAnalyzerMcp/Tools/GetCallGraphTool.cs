using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetCallGraphTool
{
    public static async Task<string> GetCallGraph(
        WorkspaceService workspace,
        string className,
        string methodName,
        string direction = "both",
        int maxDepth = 1,
        int maxCallers = 30)
    {
        var types = workspace.FindTypesByName(className);

        if (types.Count == 0)
            return $"Type '{className}' not found.";

        if (types.Count > 1)
        {
            return ToonFormat.Toon.Encode(new
            {
                error = "ambiguous",
                message = $"Found {types.Count} types matching '{className}':",
                matches = types.Select(t => t.ToDisplayString()).ToList()
            });
        }

        var type = types[0];
        var typeTree = type.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree;
        var compilation = typeTree != null ? workspace.GetCompilationForTree(typeTree) : workspace.Compilation;
        var solution = workspace.Project.Solution;

        var methods = type.GetMembers(methodName).OfType<IMethodSymbol>().ToList();
        if (methods.Count == 0)
            return $"Method '{methodName}' not found in '{type.Name}'.";

        var method = methods[0];

        List<object>? callers = null;
        if (direction is "callers" or "both")
        {
            var callerRefs = await SymbolFinder.FindCallersAsync(method, solution);
            callers = callerRefs
                .Where(c => c.IsDirect)
                .Take(maxCallers)
                .Select(c =>
                {
                    var (path, line) = workspace.GetSymbolLocation(c.CallingSymbol);
                    return (object)new
                    {
                        name = c.CallingSymbol.Name,
                        containingType = c.CallingSymbol.ContainingType?.Name,
                        fullName = c.CallingSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        kind = c.CallingSymbol.Kind.ToString().ToLowerInvariant(),
                        filePath = path,
                        line
                    };
                })
                .ToList();
        }

        List<object>? callees = null;
        if (direction is "callees" or "both")
        {
            callees = [];
            CollectCallees(method, compilation, workspace, callees, maxDepth, 0, new HashSet<ISymbol>(SymbolEqualityComparer.Default));
        }

        var (methodPath, methodLine) = workspace.GetSymbolLocation(method);

        return ToonFormat.Toon.Encode(new
        {
            method = new
            {
                name = method.Name,
                containingType = method.ContainingType?.Name,
                fullSignature = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                filePath = methodPath,
                line = methodLine
            },
            callerCount = callers?.Count,
            callers,
            calleeCount = callees?.Count,
            callees
        });
    }

    [McpServerTool(Name = "get_call_graph"),
     Description("Get the call graph for a method: what methods it calls (callees) and what calls it (callers). " +
                 "Useful for impact analysis before refactoring. " +
                 "Example: get_call_graph(className='CleaningSquadService', methodName='Initialize')")]
    public static async Task<string> McpGetCallGraph(
        WorkspaceService workspace,
        [Description("Class containing the method.")]
        string className,
        [Description("Method name to analyze.")]
        string methodName,
        [Description("Direction: 'callers' (who calls this), 'callees' (what this calls), 'both'. Default: 'both'")]
        string direction = "both",
        [Description("Maximum depth for callee traversal. Default: 1")]
        int maxDepth = 1,
        [Description("Maximum number of callers to return. Default: 30")]
        int maxCallers = 30)
    {
        await workspace.EnsureLoadedAsync();
        return await GetCallGraph(workspace, className, methodName, direction, maxDepth, maxCallers);
    }

    private static void CollectCallees(IMethodSymbol method, Compilation _,
        WorkspaceService workspace, List<object> results, int maxDepth, int currentDepth,
        HashSet<ISymbol> visited)
    {
        if (currentDepth > maxDepth) return;
        if (!visited.Add(method)) return;

        foreach (var location in method.Locations.Where(l => l.IsInSource))
        {
            var tree = location.SourceTree;
            if (tree == null) continue;

            var comp = workspace.GetCompilationForTree(tree);
            var root = tree.GetRoot();
            var methodNode = root.FindNode(location.SourceSpan);
            var semanticModel = comp.GetSemanticModel(tree);

            foreach (var invocation in methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                try
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                    var calledSymbol = symbolInfo.Symbol as IMethodSymbol;
                    if (calledSymbol == null) continue;

                    if (calledSymbol.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor
                        or MethodKind.LocalFunction or MethodKind.ReducedExtension))
                        continue;

                    var (path, line) = workspace.GetSymbolLocation(calledSymbol);
                    var isExternal = path == null;

                    results.Add(new
                    {
                        name = calledSymbol.Name,
                        containingType = calledSymbol.ContainingType?.Name,
                        fullName = calledSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        isExternal,
                        depth = currentDepth,
                        filePath = path,
                        line
                    });

                    if (currentDepth < maxDepth && !isExternal)
                    {
                        CollectCallees(calledSymbol, _, workspace, results,
                            maxDepth, currentDepth + 1, visited);
                    }
                }
                catch
                {
                    // Skip invocations that Roslyn cannot resolve
                    // (e.g. await expressions, string interpolation handlers, complex generics)
                }
            }
        }
    }
}
