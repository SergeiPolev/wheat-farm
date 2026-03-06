using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class FindUnusedSymbolsTool
{
    public static async Task<string> FindUnused(
        WorkspaceService workspace,
        string namespaceName,
        string symbolKind = "class",
        string accessibility = "all",
        bool includeNested = true,
        int maxResults = 50)
    {
        var solution = workspace.Project.Solution;

        var symbols = new List<ISymbol>();

        var nsFilteredTypes = workspace.GetAllSourceTypes()
            .Where(t =>
            {
                var ns = t.ContainingNamespace?.ToDisplayString() ?? "";
                if (includeNested)
                    return ns == namespaceName || ns.StartsWith(namespaceName + ".");
                return ns == namespaceName;
            })
            .Where(t => t.ContainingType == null)
            .ToList();

        if (symbolKind is "class" or "all")
        {
            symbols.AddRange(nsFilteredTypes.Where(t => MatchesAccessibility(t, accessibility)));
        }

        if (symbolKind is "method" or "property" or "field" or "all")
        {
            foreach (var type in nsFilteredTypes)
            {
                var members = type.GetMembers()
                    .Where(m => !m.IsImplicitlyDeclared)
                    .Where(m => MatchesSymbolKind(m, symbolKind))
                    .Where(m => MatchesAccessibility(m, accessibility))
                    .Where(m => m is not IMethodSymbol { MethodKind: not MethodKind.Ordinary });

                symbols.AddRange(members);
            }
        }

        if (symbols.Count == 0)
            return $"No symbols found in namespace '{namespaceName}' matching kind='{symbolKind}'";

        var unused = new List<object>();
        var checkedCount = 0;

        foreach (var symbol in symbols)
        {
            if (unused.Count >= maxResults) break;

            if (ShouldSkip(symbol)) continue;

            checkedCount++;
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution);
            var refCount = references.Sum(r => r.Locations.Count());

            if (refCount == 0)
            {
                var (path, line) = workspace.GetSymbolLocation(symbol);
                unused.Add(new
                {
                    name = symbol.Name,
                    kind = symbol.Kind.ToString().ToLowerInvariant(),
                    containingType = symbol.ContainingType?.Name,
                    fullName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    accessibility = symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
                    filePath = path,
                    line
                });
            }
        }

        return ToonFormat.Toon.Encode(new
        {
            @namespace = namespaceName,
            symbolKind,
            totalChecked = checkedCount,
            unusedCount = unused.Count,
            note = "May have false positives for: DI-registered types, Unity serialized fields, " +
                   "types used via reflection, interface implementations, MonoBehaviour callbacks",
            unused
        });
    }

    [McpServerTool(Name = "find_unused_symbols"),
     Description("Find potentially unused types or members (dead code detection). " +
                 "Searches for symbols with zero references in the project. " +
                 "WARNING: May have false positives for types used via reflection, DI, or Unity serialization. " +
                 "Example: find_unused_symbols(namespaceName='Game.CleaningSquad', symbolKind='class')")]
    public static async Task<string> McpFindUnused(
        WorkspaceService workspace,
        [Description("Filter to symbols in this namespace. Required to keep scan manageable.")]
        string namespaceName,
        [Description("Kind of symbol to check: 'class', 'method', 'property', 'field', or 'all'. Default: 'class'")]
        string symbolKind = "class",
        [Description("Filter by accessibility: 'public', 'internal', 'private', or 'all'. Default: 'all'")]
        string accessibility = "all",
        [Description("If true, include nested sub-namespaces. Default: true")]
        bool includeNested = true,
        [Description("Maximum number of results. Default: 50")]
        int maxResults = 50)
    {
        await workspace.EnsureLoadedAsync();
        return await FindUnused(workspace, namespaceName, symbolKind, accessibility, includeNested, maxResults);
    }

    private static bool MatchesAccessibility(ISymbol symbol, string accessibility)
    {
        if (accessibility == "all") return true;
        return accessibility.ToLowerInvariant() switch
        {
            "public" => symbol.DeclaredAccessibility == Accessibility.Public,
            "internal" => symbol.DeclaredAccessibility == Accessibility.Internal,
            "private" => symbol.DeclaredAccessibility == Accessibility.Private,
            "protected" => symbol.DeclaredAccessibility == Accessibility.Protected,
            _ => true
        };
    }

    private static readonly HashSet<string> UnityCallbacks =
    [
        "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
        "OnEnable", "OnDisable", "OnDestroy", "OnApplicationPause",
        "OnApplicationQuit", "OnGUI", "OnTriggerEnter", "OnTriggerExit",
        "OnCollisionEnter", "OnCollisionExit", "OnValidate", "Reset"
    ];

    private static bool ShouldSkip(ISymbol symbol)
    {
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }) return true;

        if (symbol is IMethodSymbol ms)
        {
            if (UnityCallbacks.Contains(ms.Name)) return true;
        }

        if (symbol is INamedTypeSymbol type)
        {
            if (type.AllInterfaces.Any(i => i.Name is "IFeatureInstaller" or "IInitializable" or "IDisposable"))
                return true;
        }

        if (symbol is IMethodSymbol { IsOverride: true }) return true;

        if (symbol.ContainingType != null && symbol is IMethodSymbol method)
        {
            foreach (var iface in symbol.ContainingType.AllInterfaces)
            {
                var ifaceMembers = iface.GetMembers(symbol.Name);
                if (ifaceMembers.Length > 0) return true;
            }
        }

        return false;
    }
}
