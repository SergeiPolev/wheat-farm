using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetConstructorDepsTool
{
    [McpServerTool(Name = "get_constructor_dependencies"),
     Description("Get constructor parameters of a class — shows what dependencies are injected via DI. " +
                 "Faster and more focused than get_class_info when you only need to know DI dependencies.")]
    public static async Task<string> McpGetDeps(
        WorkspaceService workspace,
        [Description("Class name to look up. Can be short name or fully-qualified.")]
        string className)
    {
        await workspace.EnsureLoadedAsync();
        return GetDeps(workspace, className);
    }

    public static string GetDeps(
        WorkspaceService workspace,
        string className)
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

        var type = types[0];
        var (path, line) = workspace.GetSymbolLocation(type);

        var constructors = type.Constructors
            .Where(c => !c.IsImplicitlyDeclared)
            .ToList();

        if (constructors.Count == 0)
        {
            return ToonFormat.Toon.Encode(new
            {
                name = type.Name,
                fullName = type.ToDisplayString(),
                filePath = path,
                line,
                constructors = Array.Empty<object>(),
                note = "No explicit constructors — uses default parameterless constructor"
            });
        }

        var result = new
        {
            name = type.Name,
            fullName = type.ToDisplayString(),
            filePath = path,
            line,
            interfaces = type.Interfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).ToArray(),
            constructors = constructors.Select(c => new
            {
                parameters = c.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    fullType = p.Type.ToDisplayString(),
                    isOptional = p.IsOptional,
                    defaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null
                }).ToArray()
            }).ToArray()
        };

        return ToonFormat.Toon.Encode(result);
    }
}
