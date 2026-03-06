using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetMethodBodyTool
{
    [McpServerTool(Name = "get_method_body"),
     Description("Get the source code of a specific method, property, or constructor. " +
                 "Returns the full declaration with body, avoiding the need to read entire files. " +
                 "Example: get_method_body(className='CleaningSquadService', methodName='Initialize')")]
    public static async Task<string> McpGetMethodBody(
        WorkspaceService workspace,
        [Description("Class containing the method. Can be short name or fully-qualified.")]
        string className,
        [Description("Method or property name. Use '.ctor' for constructors. " +
                     "Can also match property getters/setters.")]
        string methodName,
        [Description("If true, include XML doc comments and attributes. Default: true")]
        bool includeDocComments = true,
        [Description("If multiple overloads exist, return all of them. Default: true")]
        bool includeOverloads = true)
    {
        await workspace.EnsureLoadedAsync();
        return GetMethodBody(workspace, className, methodName, includeDocComments, includeOverloads);
    }

    public static string GetMethodBody(
        WorkspaceService workspace,
        string className,
        string methodName,
        bool includeDocComments = true,
        bool includeOverloads = true)
    {
        var types = workspace.FindTypesByName(className);

        if (types.Count == 0)
            return $"Type '{className}' not found. Try search_symbols to find it.";

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
        var results = new List<object>();

        foreach (var location in type.Locations.Where(l => l.IsInSource))
        {
            var tree = location.SourceTree;
            if (tree == null) continue;

            var root = tree.GetRoot();
            var typeDecl = root.FindNode(location.SourceSpan)
                .AncestorsAndSelf()
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault();

            if (typeDecl == null) continue;

            var isConstructor = methodName is ".ctor" or "ctor" ||
                                methodName.Equals(type.Name, StringComparison.OrdinalIgnoreCase);

            if (isConstructor)
            {
                foreach (var ctor in typeDecl.Members.OfType<ConstructorDeclarationSyntax>())
                {
                    results.Add(FormatMember(ctor, tree, includeDocComments));
                    if (!includeOverloads) break;
                }
            }
            else
            {
                foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Identifier.Text.Equals(methodName, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(FormatMember(method, tree, includeDocComments));
                    if (!includeOverloads) break;
                }

                foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>()
                    .Where(p => p.Identifier.Text.Equals(methodName, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(FormatMember(prop, tree, includeDocComments));
                }

                if (results.Count == 0)
                {
                    foreach (var field in typeDecl.Members.OfType<FieldDeclarationSyntax>())
                    {
                        foreach (var variable in field.Declaration.Variables)
                        {
                            if (variable.Identifier.Text.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                            {
                                results.Add(FormatMember(field, tree, includeDocComments));
                            }
                        }
                    }
                }
            }
        }

        if (results.Count == 0)
            return $"Member '{methodName}' not found in '{type.Name}'. " +
                   $"Available members: {string.Join(", ", type.GetMembers().Where(m => !m.IsImplicitlyDeclared).Select(m => m.Name).Distinct().Take(20))}";

        return ToonFormat.Toon.Encode(new
        {
            className = type.ToDisplayString(),
            memberName = methodName,
            overloads = results.Count,
            members = results
        });
    }

    private static object FormatMember(SyntaxNode node, SyntaxTree tree, bool includeDocComments)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var startLine = lineSpan.StartLinePosition.Line + 1;
        var endLine = lineSpan.EndLinePosition.Line + 1;

        string sourceCode;
        if (includeDocComments)
        {
            var fullSpan = node.FullSpan;
            sourceCode = node.SyntaxTree.GetText().GetSubText(fullSpan).ToString().TrimStart('\r', '\n');
        }
        else
        {
            sourceCode = node.ToString().TrimStart('\r', '\n');
        }

        var lines = sourceCode.Split('\n');
        var minIndent = lines
            .Where(l => l.Trim().Length > 0)
            .Select(l => l.Length - l.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        if (minIndent > 0)
        {
            sourceCode = string.Join('\n', lines.Select(l =>
                l.Length >= minIndent ? l[minIndent..] : l));
        }

        return new
        {
            filePath = tree.FilePath,
            startLine,
            endLine,
            lineCount = endLine - startLine + 1,
            sourceCode = sourceCode.TrimEnd()
        };
    }
}
