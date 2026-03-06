using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetClassComplexityTool
{
    public static string GetClassComplexity(
        WorkspaceService workspace,
        string className,
        string sortBy = "complexity")
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
        var methodMetrics = new List<MethodMetric>();
        int totalLines = 0;

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

            var compilation = workspace.GetCompilationForTree(tree);
            var semanticModel = compilation.GetSemanticModel(tree);

            var typeSpan = typeDecl.GetLocation().GetLineSpan();
            totalLines += typeSpan.EndLinePosition.Line - typeSpan.StartLinePosition.Line + 1;

            foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var complexity = CalculateCyclomaticComplexity(method, semanticModel);
                var methodSpan = method.GetLocation().GetLineSpan();
                var lines = methodSpan.EndLinePosition.Line - methodSpan.StartLinePosition.Line + 1;

                methodMetrics.Add(new MethodMetric
                {
                    Name = method.Identifier.Text,
                    Parameters = method.ParameterList.Parameters.Count,
                    Lines = lines,
                    Complexity = complexity,
                    IsAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword),
                    Accessibility = method.Modifiers.ToString(),
                    StartLine = methodSpan.StartLinePosition.Line + 1
                });
            }

            foreach (var ctor in typeDecl.Members.OfType<ConstructorDeclarationSyntax>())
            {
                var complexity = CalculateCyclomaticComplexity(ctor, semanticModel);
                var ctorSpan = ctor.GetLocation().GetLineSpan();
                var lines = ctorSpan.EndLinePosition.Line - ctorSpan.StartLinePosition.Line + 1;

                methodMetrics.Add(new MethodMetric
                {
                    Name = ".ctor",
                    Parameters = ctor.ParameterList.Parameters.Count,
                    Lines = lines,
                    Complexity = complexity,
                    IsAsync = false,
                    Accessibility = ctor.Modifiers.ToString(),
                    StartLine = ctorSpan.StartLinePosition.Line + 1
                });
            }
        }

        var sorted = sortBy switch
        {
            "lines" => methodMetrics.OrderByDescending(m => m.Lines),
            "name" => methodMetrics.OrderBy(m => m.Name),
            _ => methodMetrics.OrderByDescending(m => m.Complexity)
        };

        var constructorParams = type.Constructors
            .Where(c => !c.IsImplicitlyDeclared)
            .SelectMany(c => c.Parameters)
            .Count();

        var fieldCount = type.GetMembers().OfType<IFieldSymbol>()
            .Count(f => !f.IsImplicitlyDeclared && !f.IsStatic);

        var methodCount = type.GetMembers().OfType<IMethodSymbol>()
            .Count(m => !m.IsImplicitlyDeclared && m.MethodKind == MethodKind.Ordinary);

        var avgComplexity = methodMetrics.Count > 0
            ? methodMetrics.Average(m => m.Complexity)
            : 0;

        var maxComplexity = methodMetrics.Count > 0
            ? methodMetrics.Max(m => m.Complexity)
            : 0;

        var score = (constructorParams * 2) + (fieldCount * 1.5) + (methodCount * 0.5) + (avgComplexity * 3) + (totalLines * 0.01);
        var rating = score switch
        {
            < 20 => "low",
            < 50 => "moderate",
            < 100 => "high",
            _ => "very high — consider refactoring"
        };

        var (typePath, typeLine) = workspace.GetSymbolLocation(type);

        return ToonFormat.Toon.Encode(new
        {
            className = type.ToDisplayString(),
            filePath = typePath,
            line = typeLine,
            classMetrics = new
            {
                totalLines,
                methodCount,
                fieldCount,
                constructorDependencies = constructorParams,
                interfaceCount = type.Interfaces.Length,
                averageComplexity = Math.Round(avgComplexity, 1),
                maxComplexity,
                complexityScore = Math.Round(score, 1),
                rating
            },
            methods = sorted.Select(m => new
            {
                name = m.Name,
                complexity = m.Complexity,
                lines = m.Lines,
                parameters = m.Parameters,
                isAsync = m.IsAsync ? true : (bool?)null,
                startLine = m.StartLine
            }).ToList()
        });
    }

    [McpServerTool(Name = "get_class_complexity"),
     Description("Analyze class complexity: cyclomatic complexity per method, total LOC, dependency count, " +
                 "and overall complexity score. Helps identify God classes that need refactoring. " +
                 "Example: get_class_complexity(className='CleaningSquadService')")]
    public static async Task<string> McpGetClassComplexity(
        WorkspaceService workspace,
        [Description("Class to analyze. Can be short name or fully-qualified.")]
        string className,
        [Description("Sort methods by: 'complexity' (highest first), 'lines' (longest first), 'name'. Default: 'complexity'")]
        string sortBy = "complexity")
    {
        await workspace.EnsureLoadedAsync();
        return GetClassComplexity(workspace, className, sortBy);
    }

    private class MethodMetric
    {
        public string Name { get; set; } = "";
        public int Parameters { get; set; }
        public int Lines { get; set; }
        public int Complexity { get; set; }
        public bool IsAsync { get; set; }
        public string Accessibility { get; set; } = "";
        public int StartLine { get; set; }
    }
}
