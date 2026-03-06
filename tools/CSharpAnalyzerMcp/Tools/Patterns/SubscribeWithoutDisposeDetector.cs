using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools.Patterns;

public sealed class SubscribeWithoutDisposeDetector : IPatternDetector
{
    public string Name => "subscribe_without_dispose";
    public string Description => "Subscribe() without .AddTo() or CompositeDisposable tracking";
    public string Group => "project_conventions";
    public string DefaultSeverity => "warning";

    public List<PatternMatch> Detect(WorkspaceService workspace, string? scope, int maxResults)
    {
        var matches = new List<PatternMatch>();

        foreach (var (tree, compilation) in workspace.GetAllSyntaxTrees())
        {
            if (matches.Count >= maxResults) break;

            var root = tree.GetRoot();
            if (!MatchesScope(tree.FilePath, scope, root)) continue;
            var model = compilation.GetSemanticModel(tree);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (matches.Count >= maxResults) break;

                var methodName = GetInvocationMethodName(invocation);
                if (methodName != "Subscribe") continue;

                if (invocation.Expression is MemberAccessExpressionSyntax ma)
                {
                    var receiverType = model.GetTypeInfo(ma.Expression).Type;
                    if (receiverType != null && !IsObservableType(receiverType)) continue;
                }

                if (HasAddToInChain(invocation)) continue;

                var lineSpan = invocation.GetLocation().GetLineSpan();
                var containingMember = GetContainingMember(invocation, model);
                var lineText = workspace.GetLineText(lineSpan.Path, lineSpan.StartLinePosition.Line + 1);

                matches.Add(new PatternMatch(
                    FilePath: lineSpan.Path,
                    Line: lineSpan.StartLinePosition.Line + 1,
                    ContainingMember: containingMember,
                    Code: lineText ?? invocation.ToString(),
                    Detail: "Subscribe() without .AddTo() — potential memory leak",
                    Severity: DefaultSeverity
                ));
            }
        }

        return matches;
    }

    private static bool HasAddToInChain(InvocationExpressionSyntax invocation)
    {
        SyntaxNode current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax parentMa)
        {
            if (parentMa.Name.Identifier.Text == "AddTo") return true;
            if (parentMa.Parent is InvocationExpressionSyntax parentInv)
                current = parentInv;
            else
                break;
        }
        return false;
    }

    private static bool IsObservableType(ITypeSymbol type)
    {
        var name = type.ToDisplayString();
        return name.Contains("Observable")
            || name.Contains("Subject")
            || name.Contains("ReactiveProperty")
            || name.Contains("ReactiveCommand")
            || name.Contains("ReactiveEvent")
            || type.AllInterfaces.Any(i =>
                i.Name == "IObservable" || i.ToDisplayString().StartsWith("System.IObservable"));
    }

}
