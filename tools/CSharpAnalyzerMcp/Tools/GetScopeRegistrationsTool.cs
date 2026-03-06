using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetScopeRegistrationsTool
{
    public static string GetScopeRegistrations(
        WorkspaceService workspace,
        string scope,
        string? filterType = null,
        int maxResults = 200)
    {
        var types = workspace.FindTypesByName(scope);
        if (types.Count == 0)
            return $"Scope '{scope}' not found. Try: 'RootScope', 'GameScope', or 'HotelScope'.";

        if (types.Count > 1)
        {
            return ToonFormat.Toon.Encode(new
            {
                error = "ambiguous",
                message = $"Found {types.Count} types matching '{scope}':",
                matches = types.Select(t => t.ToDisplayString()).ToList()
            });
        }

        var scopeType = types[0];
        var scopeTree = scopeType.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree;
        var compilation = scopeTree != null ? workspace.GetCompilationForTree(scopeTree) : workspace.Compilation;
        var parser = new DiRegistrationParser(compilation);

        var directRegistrations = parser.ParseInstaller(scopeType);

        var featureInstallers = FindFeatureInstallers(scopeType, compilation);

        var featureRegistrations = new Dictionary<string, List<DiRegistration>>();
        foreach (var installer in featureInstallers)
        {
            var installerTypes = workspace.FindTypesByName(installer);
            if (installerTypes.Count > 0)
            {
                var regs = parser.ParseInstaller(installerTypes[0]);
                if (regs.Count > 0)
                {
                    featureRegistrations[installer] = regs;
                }
            }
        }

        var allRegistrations = new List<(string source, DiRegistration reg)>();

        foreach (var reg in directRegistrations)
        {
            allRegistrations.Add((scope, reg));
        }

        foreach (var (installer, regs) in featureRegistrations)
        {
            foreach (var reg in regs)
            {
                allRegistrations.Add((installer, reg));
            }
        }

        if (!string.IsNullOrEmpty(filterType))
        {
            allRegistrations = allRegistrations.Where(r =>
                r.reg.RegisteredType?.Contains(filterType, StringComparison.OrdinalIgnoreCase) == true
                || r.reg.InterfaceType?.Contains(filterType, StringComparison.OrdinalIgnoreCase) == true
            ).ToList();
        }

        var (scopePath, scopeLine) = workspace.GetSymbolLocation(scopeType);

        var results = allRegistrations
            .Take(maxResults)
            .Select(r => new
            {
                source = r.source,
                kind = r.reg.RegistrationKind,
                registeredType = r.reg.RegisteredType,
                interfaceType = r.reg.InterfaceType,
                lifetime = r.reg.Lifetime,
                section = r.reg.Section,
                registeredAs = r.reg.RegisteredAs.Count > 0 ? r.reg.RegisteredAs : null,
                isEntryPoint = r.reg.IsEntryPoint ? true : (bool?)null,
                line = r.reg.Line
            })
            .ToList();

        return ToonFormat.Toon.Encode(new
        {
            scope,
            scopeFile = scopePath,
            scopeLine,
            featureInstallersFound = featureInstallers.Count,
            featureInstallers = featureInstallers.Count > 0 ? featureInstallers : null,
            directRegistrationCount = directRegistrations.Count,
            totalRegistrations = allRegistrations.Count,
            showing = results.Count,
            registrations = results
        });
    }

    [McpServerTool(Name = "get_scope_registrations"),
     Description("Get ALL DI registrations in a specific scope (RootScope, GameScope, or HotelScope). " +
                 "Parses the scope's Configure method and all FeatureBuilder.AddFeature<T>() calls to show " +
                 "every type registered at that scope level. " +
                 "Example: get_scope_registrations(scope='GameScope')")]
    public static async Task<string> McpGetScopeRegistrations(
        WorkspaceService workspace,
        [Description("Scope to analyze: 'RootScope', 'GameScope', or 'HotelScope'")]
        string scope,
        [Description("Filter to registrations involving this type name. Default: null (show all)")]
        string? filterType = null,
        [Description("Maximum results. Default: 200")]
        int maxResults = 200)
    {
        await workspace.EnsureLoadedAsync();
        return GetScopeRegistrations(workspace, scope, filterType, maxResults);
    }

    private static List<string> FindFeatureInstallers(INamedTypeSymbol scopeType, Compilation compilation)
    {
        var installers = new List<string>();

        foreach (var location in scopeType.Locations.Where(l => l.IsInSource))
        {
            var tree = location.SourceTree;
            if (tree == null) continue;

            var root = tree.GetRoot();

            foreach (var invocation in root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>())
            {
                var expr = invocation.Expression;

                if (expr is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax ma
                    && ma.Name is Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax gn
                    && gn.Identifier.Text == "AddFeature")
                {
                    foreach (var typeArg in gn.TypeArgumentList.Arguments)
                    {
                        installers.Add(typeArg.ToString());
                    }
                }
            }
        }

        return installers.Distinct().OrderBy(i => i).ToList();
    }
}
