using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace CSharpAnalyzerMcp.Services;

public static class SyntaxHelper
{
    public static string? GetContainingMember(SyntaxNode node, SemanticModel model)
    {
        var memberDecl = node.Ancestors().FirstOrDefault(n => n is MemberDeclarationSyntax);
        if (memberDecl == null) return null;
        var symbol = model.GetDeclaredSymbol(memberDecl);
        return symbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    public static bool MatchesScope(string filePath, string? scope, SyntaxNode root)
    {
        if (string.IsNullOrEmpty(scope)) return true;
        if (filePath.Contains(scope, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var nsDecl in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            if (nsDecl.Name.ToString().StartsWith(scope, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string? GetSymbolType(ISymbol symbol)
    {
        return symbol switch
        {
            ILocalSymbol l => l.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            IParameterSymbol p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            IFieldSymbol f => f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            IPropertySymbol prop => prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            _ => null
        };
    }

    public static string? GetInvocationMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => null
        };
    }

    public static bool IsMonoBehaviour(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.Name == "MonoBehaviour") return true;
            current = current.BaseType;
        }
        return false;
    }

    public static int CalculateCyclomaticComplexity(SyntaxNode node, SemanticModel? model = null)
    {
        if (model != null)
        {
            try
            {
                var bodyNode = node switch
                {
                    MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
                    ConstructorDeclarationSyntax c => (SyntaxNode?)c.Body ?? c.ExpressionBody,
                    AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody,
                    _ => node
                };

                if (bodyNode != null)
                {
                    var bodyOp = model.GetOperation(bodyNode);
                    if (bodyOp is IBlockOperation blockOp)
                    {
                        var cfg = ControlFlowGraph.Create(blockOp);
                        var conditionalBranches = cfg.Blocks
                            .Count(b => b.ConditionalSuccessor != null && b.FallThroughSuccessor != null);
                        return conditionalBranches + 1;
                    }
                }
            }
            catch
            {
            }
        }

        return CalculateCyclomaticComplexitySyntax(node);
    }

    private static int CalculateCyclomaticComplexitySyntax(SyntaxNode node)
    {
        int complexity = 1;

        foreach (var descendant in node.DescendantNodes())
        {
            complexity += descendant switch
            {
                IfStatementSyntax => 1,
                ElseClauseSyntax => 0,
                WhileStatementSyntax => 1,
                ForStatementSyntax => 1,
                ForEachStatementSyntax => 1,
                DoStatementSyntax => 1,
                CaseSwitchLabelSyntax => 1,
                CasePatternSwitchLabelSyntax => 1,
                SwitchExpressionArmSyntax => 1,
                ConditionalExpressionSyntax => 1,
                CatchClauseSyntax => 1,
                BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression) => 1,
                BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalOrExpression) => 1,
                BinaryExpressionSyntax b when b.IsKind(SyntaxKind.CoalesceExpression) => 1,
                ConditionalAccessExpressionSyntax => 1,
                _ => 0
            };
        }

        return complexity;
    }

    public static bool MatchesSymbolKind(ISymbol symbol, string kind)
    {
        if (kind == "all") return true;
        return kind switch
        {
            "method" => symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary },
            "property" => symbol is IPropertySymbol,
            "field" => symbol is IFieldSymbol,
            _ => true
        };
    }
}
