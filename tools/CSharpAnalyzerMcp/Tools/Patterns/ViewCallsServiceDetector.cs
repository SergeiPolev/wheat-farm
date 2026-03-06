using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools.Patterns;

public sealed class ViewCallsServiceDetector : IPatternDetector
{
    public string Name => "view_calls_service";
    public string Description => "View class directly invokes a service method (MVP violation)";
    public string Group => "project_conventions";
    public string DefaultSeverity => "error";

    public List<PatternMatch> Detect(WorkspaceService workspace, string? scope, int maxResults)
    {
        var matches = new List<PatternMatch>();

        foreach (var (tree, compilation) in workspace.GetAllSyntaxTrees())
        {
            if (matches.Count >= maxResults) break;

            var root = tree.GetRoot();
            if (!MatchesScope(tree.FilePath, scope, root)) continue;
            var model = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (matches.Count >= maxResults) break;

                var typeSymbol = model.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol == null) continue;

                if (!IsViewClass(typeSymbol)) continue;

                foreach (var invocation in typeDecl.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (matches.Count >= maxResults) break;

                    var calledSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (calledSymbol?.ContainingType == null) continue;

                    if (!IsServiceType(calledSymbol.ContainingType)) continue;

                    var lineSpan = invocation.GetLocation().GetLineSpan();
                    var containingMember = GetContainingMember(invocation, model);
                    var code = invocation.ToString();
                    if (code.Length > 120) code = code[..120] + "...";

                    matches.Add(new PatternMatch(
                        FilePath: lineSpan.Path,
                        Line: lineSpan.StartLinePosition.Line + 1,
                        ContainingMember: containingMember,
                        Code: code,
                        Detail: $"View '{typeSymbol.Name}' directly calls '{calledSymbol.ContainingType.Name}.{calledSymbol.Name}()' — violates MVP pattern",
                        Severity: DefaultSeverity
                    ));
                }
            }
        }

        return matches;
    }

    private static bool IsViewClass(INamedTypeSymbol type) =>
        type.Name.EndsWith("View") && IsMonoBehaviour(type);

    private static bool IsServiceType(INamedTypeSymbol type)
    {
        if (type.Name.EndsWith("Service")) return true;
        return type.AllInterfaces.Any(i => i.Name.EndsWith("Service"));
    }

}
