using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetDiRegistrationsTool
{
    [McpServerTool(Name = "get_di_registrations"),
     Description("Parse VContainer DI registrations from Installer files. Shows how types are registered: " +
                 "Register, RegisterInstance, RegisterEntryPoint, UseEntryPoints patterns. " +
                 "Can search for a specific type's registration or list all registrations in an Installer. " +
                 "Example: get_di_registrations(installerName='CleaningSquadInstaller') shows all registrations.")]
    public static async Task<string> McpGetDiRegistrations(
        WorkspaceService workspace,
        [Description("Name of the Installer class to parse. " +
                     "Examples: 'CleaningSquadInstaller', 'GameScope', 'RootScope'")]
        string? installerName = null,
        [Description("Filter to registrations involving this type name. " +
                     "Example: 'CleaningSquadService' to find where it's registered")]
        string? typeName = null,
        [Description("Maximum number of results. Default: 100")]
        int maxResults = 100)
    {
        await workspace.EnsureLoadedAsync();
        return GetDiRegistrations(workspace, installerName, typeName, maxResults);
    }

    public static string GetDiRegistrations(
        WorkspaceService workspace,
        string? installerName = null,
        string? typeName = null,
        int maxResults = 100)
    {
        var parser = new DiRegistrationParser(workspace.Compilation);

        List<DiRegistration> registrations;

        if (!string.IsNullOrEmpty(installerName))
        {
            var types = workspace.FindTypesByName(installerName);
            if (types.Count == 0)
                return $"Installer '{installerName}' not found. Try search_symbols with '*Installer' or '*Scope'.";

            if (types.Count > 1)
            {
                return ToonFormat.Toon.Encode(new
                {
                    error = "ambiguous",
                    message = $"Found {types.Count} types matching '{installerName}':",
                    matches = types.Select(t => t.ToDisplayString()).ToList()
                });
            }

            registrations = parser.ParseInstaller(types[0]);

            if (!string.IsNullOrEmpty(typeName))
            {
                registrations = registrations.Where(r =>
                    r.RegisteredType?.Contains(typeName, StringComparison.OrdinalIgnoreCase) == true
                    || r.InterfaceType?.Contains(typeName, StringComparison.OrdinalIgnoreCase) == true
                    || r.RegisteredAs.Any(a => a.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
        }
        else if (!string.IsNullOrEmpty(typeName))
        {
            registrations = parser.ParseAllInstallers(typeName);
        }
        else
        {
            return "Please specify either installerName or typeName (or both).";
        }

        if (registrations.Count == 0)
        {
            var message = !string.IsNullOrEmpty(installerName)
                ? $"No DI registrations found in '{installerName}'"
                : $"No DI registrations found for type '{typeName}'";
            return !string.IsNullOrEmpty(typeName)
                ? $"{message}. The type might be registered via a factory or in a different assembly."
                : $"{message}. The installer might use patterns not yet supported by the parser.";
        }

        var results = registrations
            .Take(maxResults)
            .Select(r => new
            {
                kind = r.RegistrationKind,
                registeredType = r.RegisteredType,
                interfaceType = r.InterfaceType,
                lifetime = r.Lifetime,
                section = r.Section,
                registeredAs = r.RegisteredAs.Count > 0 ? r.RegisteredAs : null,
                isEntryPoint = r.IsEntryPoint ? true : (bool?)null,
                filePath = r.FilePath,
                line = r.Line
            })
            .ToList();

        return ToonFormat.Toon.Encode(new
        {
            totalRegistrations = registrations.Count,
            showing = results.Count,
            registrations = results
        });
    }
}
