using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetClassInfoTool
{
    [McpServerTool(Name = "get_class_info"),
     Description("Get detailed information about a C# class/struct/interface: base types, interfaces, " +
                 "constructor parameters (DI dependencies), fields, properties, methods, events, nested types, " +
                 "and file location. Use short name (e.g. 'CleaningSquadService') or fully-qualified name.")]
    public static async Task<string> McpGetClassInfo(
        WorkspaceService workspace,
        [Description("Class name to look up. Can be short name or fully-qualified (e.g. 'Game.CleaningSquad.CleaningSquadService')")]
        string className,
        [Description("Level of detail: 'summary' (types and signatures only) or 'full' (includes all members). Default: 'full'")]
        string depth = "full")
    {
        await workspace.EnsureLoadedAsync();
        return GetClassInfo(workspace, className, depth);
    }

    public static string GetClassInfo(
        WorkspaceService workspace,
        string className,
        string depth = "full")
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
        return FormatTypeInfo(workspace, type, depth == "full");
    }

    private static string FormatTypeInfo(WorkspaceService workspace, INamedTypeSymbol type, bool full)
    {
        var (path, line) = workspace.GetSymbolLocation(type);

        var constructors = type.Constructors
            .Where(c => !c.IsImplicitlyDeclared)
            .Select(c => new
            {
                parameters = c.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                }).ToArray()
            })
            .ToArray();

        var baseType = type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object
            ? type.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            : null;

        var interfaces = type.Interfaces
            .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            .ToArray();

        var allInterfaces = type.AllInterfaces
            .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            .ToArray();

        object? members = null;
        if (full)
        {
            var fields = type.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => !f.IsImplicitlyDeclared)
                .Select(f => new
                {
                    name = f.Name,
                    type = f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    accessibility = f.DeclaredAccessibility.ToString().ToLowerInvariant(),
                    isStatic = f.IsStatic,
                    isReadOnly = f.IsReadOnly,
                    attributes = f.GetAttributes().Select(a => a.AttributeClass?.Name ?? "").Where(n => n != "").ToArray()
                })
                .ToArray();

            var properties = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => !p.IsImplicitlyDeclared)
                .Select(p => new
                {
                    name = p.Name,
                    type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    accessibility = p.DeclaredAccessibility.ToString().ToLowerInvariant(),
                    hasGetter = p.GetMethod != null,
                    hasSetter = p.SetMethod != null,
                    isStatic = p.IsStatic
                })
                .ToArray();

            var methods = type.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => !m.IsImplicitlyDeclared && m.MethodKind == MethodKind.Ordinary)
                .Select(m => new
                {
                    name = m.Name,
                    returnType = m.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    parameters = m.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}").ToArray(),
                    accessibility = m.DeclaredAccessibility.ToString().ToLowerInvariant(),
                    isStatic = m.IsStatic,
                    isAsync = m.IsAsync,
                    isVirtual = m.IsVirtual,
                    isOverride = m.IsOverride,
                    isAbstract = m.IsAbstract
                })
                .ToArray();

            var events = type.GetMembers()
                .OfType<IEventSymbol>()
                .Select(e => new
                {
                    name = e.Name,
                    type = e.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    accessibility = e.DeclaredAccessibility.ToString().ToLowerInvariant()
                })
                .ToArray();

            var nestedTypes = type.GetTypeMembers()
                .Select(t => new
                {
                    name = t.Name,
                    kind = t.TypeKind.ToString().ToLowerInvariant(),
                    accessibility = t.DeclaredAccessibility.ToString().ToLowerInvariant()
                })
                .ToArray();

            members = new
            {
                fields = fields.Length > 0 ? fields : null,
                properties = properties.Length > 0 ? properties : null,
                methods = methods.Length > 0 ? methods : null,
                events = events.Length > 0 ? events : null,
                nestedTypes = nestedTypes.Length > 0 ? nestedTypes : null
            };
        }

        var result = new
        {
            name = type.Name,
            fullName = type.ToDisplayString(),
            @namespace = type.ContainingNamespace?.ToDisplayString() ?? "",
            kind = type.TypeKind.ToString().ToLowerInvariant(),
            accessibility = type.DeclaredAccessibility.ToString().ToLowerInvariant(),
            isAbstract = type.IsAbstract,
            isSealed = type.IsSealed,
            isStatic = type.IsStatic,
            baseType,
            interfaces = interfaces.Length > 0 ? interfaces : null,
            allInterfaces = allInterfaces.Length > 0 ? allInterfaces : null,
            constructors = constructors.Length > 0 ? constructors : null,
            members,
            filePath = path,
            line
        };

        return ToonFormat.Toon.Encode(result);
    }
}
