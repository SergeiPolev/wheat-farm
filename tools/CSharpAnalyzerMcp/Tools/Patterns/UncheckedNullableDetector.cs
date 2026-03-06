using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools.Patterns;

public sealed class UncheckedNullableDetector : IPatternDetector
{
    public string Name => "unchecked_nullable";
    public string Description => "Access to Nullable<T>.Value without HasValue/null check";
    public string Group => "null_safety";
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

            foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (matches.Count >= maxResults) break;
                if (memberAccess.Name.Identifier.Text != "Value") continue;

                var typeInfo = model.GetTypeInfo(memberAccess.Expression);
                var type = typeInfo.Type;
                if (type == null) continue;

                var isNullableValue = type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                if (!isNullableValue) continue;

                if (typeInfo.Nullability.FlowState == NullableFlowState.NotNull)
                    continue;

                var hasCheck = HasNullCheckInScope(memberAccess);

                if (!hasCheck)
                {
                    var lineSpan = memberAccess.GetLocation().GetLineSpan();
                    var containingMember = GetContainingMember(memberAccess, model);
                    var code = memberAccess.Parent?.ToString() ?? memberAccess.ToString();
                    if (code.Length > 120) code = code[..120] + "...";

                    matches.Add(new PatternMatch(
                        FilePath: lineSpan.Path,
                        Line: lineSpan.StartLinePosition.Line + 1,
                        ContainingMember: containingMember,
                        Code: code,
                        Detail: $"{memberAccess.Expression} is {type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}, no HasValue check in enclosing scope",
                        Severity: DefaultSeverity
                    ));
                }
            }
        }

        return matches;
    }

    private static bool HasNullCheckInScope(MemberAccessExpressionSyntax valueAccess)
    {
        var variableName = valueAccess.Expression.ToString();

        foreach (var ancestor in valueAccess.Ancestors())
        {
            if (ancestor is ConditionalExpressionSyntax ternary)
            {
                var condition = ternary.Condition.ToString();
                if (condition.Contains($"{variableName}.HasValue")
                    || condition.Contains($"{variableName} != null")
                    || condition.Contains($"{variableName} is not null"))
                    return true;
            }

            if (ancestor is IfStatementSyntax ifStatement)
            {
                var condition = ifStatement.Condition.ToString();
                var isNullCheck = condition.Contains($"{variableName}.HasValue")
                    || condition.Contains($"{variableName} != null")
                    || condition.Contains($"{variableName} is not null");
                var isNegatedNullCheck = condition.Contains($"!{variableName}.HasValue")
                    || condition.Contains($"{variableName} == null")
                    || condition.Contains($"{variableName} is null");

                if (isNullCheck && !isNegatedNullCheck)
                {
                    if (ifStatement.Statement.Span.Contains(valueAccess.Span))
                        return true;
                }

                if (isNegatedNullCheck)
                {
                    if (AlwaysExits(ifStatement.Statement)
                        && !ifStatement.Statement.Span.Contains(valueAccess.Span))
                        return true;
                }
            }

            if (ancestor is MemberDeclarationSyntax) break;
        }

        return false;
    }

    private static bool AlwaysExits(StatementSyntax statement)
    {
        if (statement is ReturnStatementSyntax or ThrowStatementSyntax
            or BreakStatementSyntax or ContinueStatementSyntax)
            return true;

        if (statement is BlockSyntax block && block.Statements.Count > 0)
            return AlwaysExits(block.Statements.Last());

        return false;
    }

}
