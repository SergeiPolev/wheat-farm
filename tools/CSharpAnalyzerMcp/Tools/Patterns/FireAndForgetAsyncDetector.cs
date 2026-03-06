using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools.Patterns;

public sealed class FireAndForgetAsyncDetector : IPatternDetector
{
    public string Name => "fire_and_forget_async";
    public string Description => "Async method called without await (Task result discarded)";
    public string Group => "async";
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

                var symbolInfo = model.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is not IMethodSymbol methodSymbol) continue;

                var returnType = methodSymbol.ReturnType;
                if (!IsTaskType(returnType)) continue;

                var operation = model.GetOperation(invocation);
                if (operation?.Parent is IAwaitOperation) continue;
                if (IsOperationResultUsed(operation)) continue;

                var lineSpan = invocation.GetLocation().GetLineSpan();
                var containingMember = GetContainingMember(invocation, model);
                var code = invocation.ToString();
                if (code.Length > 120) code = code[..120] + "...";

                matches.Add(new PatternMatch(
                    FilePath: lineSpan.Path,
                    Line: lineSpan.StartLinePosition.Line + 1,
                    ContainingMember: containingMember,
                    Code: code,
                    Detail: $"Returns {returnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} but result is not awaited or stored",
                    Severity: DefaultSeverity
                ));
            }
        }

        return matches;
    }

    private static bool IsTaskType(ITypeSymbol type)
    {
        var name = type.ToDisplayString();
        return name.StartsWith("System.Threading.Tasks.Task")
            || name.StartsWith("System.Threading.Tasks.ValueTask")
            || name.StartsWith("Cysharp.Threading.Tasks.UniTask");
    }

    private static bool IsOperationResultUsed(IOperation? operation)
    {
        if (operation == null) return false;

        var parent = operation.Parent;
        while (parent is IConversionOperation or IParenthesizedOperation)
            parent = parent.Parent;

        return parent is IAwaitOperation
            or IAssignmentOperation
            or IVariableInitializerOperation
            or IReturnOperation
            or IArgumentOperation
            or IConditionalOperation;
    }

}
