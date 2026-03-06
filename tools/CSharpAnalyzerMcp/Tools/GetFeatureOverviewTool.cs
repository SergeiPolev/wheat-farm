using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetFeatureOverviewTool
{
    public static string GetFeatureOverview(
        WorkspaceService workspace,
        string featureName)
    {
        var namespaceName = FindFeatureNamespace(workspace, featureName);
        if (namespaceName == null)
            return $"Could not find a namespace matching '{featureName}'. Try get_namespace_types or search_symbols.";

        var allTypes = workspace.GetAllSourceTypes()
            .Where(t =>
            {
                var ns = t.ContainingNamespace?.ToDisplayString() ?? "";
                return (ns == namespaceName || ns.StartsWith(namespaceName + "."))
                    && t.ContainingType == null;
            })
            .ToList();

        if (allTypes.Count == 0)
            return $"No types found in namespace '{namespaceName}'.";

        var installers = allTypes.Where(t =>
            t.AllInterfaces.Any(i => i.Name == "IFeatureInstaller")
            || t.Name.EndsWith("Installer")).ToList();

        var views = allTypes.Where(t =>
            t.Name.EndsWith("View") && IsMonoBehaviour(t)).ToList();

        var presenters = allTypes.Where(t =>
            t.Name.EndsWith("Presenter") || t.Name.EndsWith("PM")).ToList();

        var services = allTypes.Where(t =>
            t.Name.EndsWith("Service") && !t.TypeKind.Equals(TypeKind.Interface)).ToList();

        var serviceInterfaces = allTypes.Where(t =>
            t.TypeKind == TypeKind.Interface
            && (t.Name.StartsWith("I") && t.Name.EndsWith("Service"))).ToList();

        var configs = allTypes.Where(t =>
            t.Name.EndsWith("Config") || t.Name.EndsWith("Configuration")).ToList();

        var enums = allTypes.Where(t => t.TypeKind == TypeKind.Enum).ToList();

        var saveRelated = allTypes.Where(t =>
            t.Name.Contains("Save") || t.Name.Contains("Data")).ToList();

        var categorized = new HashSet<INamedTypeSymbol>(
            installers.Concat(views).Concat(presenters).Concat(services)
                .Concat(serviceInterfaces).Concat(configs).Concat(enums).Concat(saveRelated),
            SymbolEqualityComparer.Default);
        var other = allTypes.Where(t => !categorized.Contains(t)).ToList();

        var parser = new DiRegistrationParser(workspace.Compilation);
        var registrations = new List<DiRegistration>();
        foreach (var installer in installers)
        {
            registrations.AddRange(parser.ParseInstaller(installer));
        }

        var subNamespaces = allTypes
            .Select(t => t.ContainingNamespace?.ToDisplayString() ?? "")
            .Where(ns => ns != namespaceName)
            .Distinct()
            .OrderBy(ns => ns)
            .ToList();

        var result = new
        {
            featureName,
            @namespace = namespaceName,
            totalTypes = allTypes.Count,
            subNamespaces = subNamespaces.Count > 0 ? subNamespaces : null,

            installers = installers.Count > 0
                ? installers.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null,

            diRegistrations = registrations.Count > 0
                ? registrations.Select(r => new
                {
                    kind = r.RegistrationKind,
                    type = r.RegisteredType,
                    @interface = r.InterfaceType,
                    lifetime = r.Lifetime,
                    section = r.Section,
                    isEntryPoint = r.IsEntryPoint ? true : (bool?)null
                }).ToList()
                : null,

            views = views.Count > 0
                ? views.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null,

            presenters = presenters.Count > 0
                ? presenters.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null,

            services = services.Count > 0
                ? services.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null,

            serviceInterfaces = serviceInterfaces.Count > 0
                ? serviceInterfaces.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null,

            configs = configs.Count > 0
                ? configs.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null,

            saveAndData = saveRelated.Count > 0
                ? saveRelated.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null,

            enums = enums.Count > 0
                ? enums.Select(t => new { name = t.Name, members = t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name).ToArray() }).ToList()
                : null,

            other = other.Count > 0
                ? other.Select(t => FormatTypeBrief(workspace, t)).ToList()
                : null
        };

        return ToonFormat.Toon.Encode(result);
    }

    [McpServerTool(Name = "get_feature_overview"),
     Description("Get a high-level overview of a feature: its Installer, all registered types, Views, Presenters, " +
                 "Services, and Config classes. Aggregates DI registrations with namespace exploration. " +
                 "Example: get_feature_overview('CleaningSquad') shows the entire feature structure.")]
    public static async Task<string> McpGetFeatureOverview(
        WorkspaceService workspace,
        [Description("Feature name or namespace. Examples: 'CleaningSquad', 'Game.CleaningSquad', " +
                     "'Leaderboard', 'BattlePass'. Will search for matching namespace and installer.")]
        string featureName)
    {
        await workspace.EnsureLoadedAsync();
        return GetFeatureOverview(workspace, featureName);
    }

    private static string? FindFeatureNamespace(WorkspaceService workspace, string featureName)
    {
        var allNamespaces = new HashSet<string>();
        foreach (var (tree, _) in workspace.GetAllSyntaxTrees())
        {
            var root = tree.GetRoot();
            foreach (var nsDecl in root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax>())
            {
                allNamespaces.Add(nsDecl.Name.ToString());
            }
        }

        if (allNamespaces.Contains(featureName)) return featureName;

        var gameNs = $"Game.{featureName}";
        if (allNamespaces.Contains(gameNs)) return gameNs;

        return allNamespaces
            .Where(ns => ns.Contains(featureName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(ns => ns.Length)
            .FirstOrDefault();
    }

    private static object FormatTypeBrief(WorkspaceService workspace, INamedTypeSymbol type)
    {
        var (path, line) = workspace.GetSymbolLocation(type);
        return new
        {
            name = type.Name,
            kind = type.TypeKind.ToString().ToLowerInvariant(),
            interfaces = type.Interfaces.Select(i => i.Name).ToArray(),
            filePath = path,
            line
        };
    }
}
