using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class GetReactiveChainsTool
{
    public static string GetReactiveChains(
        WorkspaceService workspace,
        string className,
        bool issuesOnly = false)
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

        var subscriptions = new List<object>();
        var reactiveFields = new List<object>();
        var potentialLeaks = new List<object>();
        var reactiveWrites = new List<object>();

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
            var model = compilation.GetSemanticModel(tree);

            foreach (var field in typeDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                var fieldTypeInfo = model.GetTypeInfo(field.Declaration.Type);
                var fieldTypeSymbol = fieldTypeInfo.Type;
                if (fieldTypeSymbol != null && IsReactiveType(fieldTypeSymbol))
                {
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var lineSpan = variable.GetLocation().GetLineSpan();
                        reactiveFields.Add(new
                        {
                            name = variable.Identifier.Text,
                            type = fieldTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            line = lineSpan.StartLinePosition.Line + 1
                        });
                    }
                }
            }

            foreach (var invocation in typeDecl.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var methodName = GetInvocationMethodName(invocation);
                if (methodName == null) continue;

                if (methodName == "Subscribe")
                {
                    var lineSpan = invocation.GetLocation().GetLineSpan();
                    var line = lineSpan.StartLinePosition.Line + 1;

                    var source = GetObservableSource(invocation);

                    var hasAddTo = HasChainedMethod(invocation, "AddTo");

                    var containingMethod = invocation.Ancestors()
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault()?.Identifier.Text
                        ?? invocation.Ancestors()
                            .OfType<ConstructorDeclarationSyntax>()
                            .FirstOrDefault()?.Identifier.Text
                        ?? "<unknown>";

                    var lineText = workspace.GetLineText(tree.FilePath, line);

                    var sub = new
                    {
                        source,
                        containingMethod,
                        hasAddTo,
                        isDisposed = hasAddTo ? "via AddTo" : (string?)null,
                        filePath = tree.FilePath,
                        line,
                        lineText
                    };

                    if (!hasAddTo)
                    {
                        potentialLeaks.Add(sub);
                    }

                    if (!issuesOnly || !hasAddTo)
                    {
                        subscriptions.Add(sub);
                    }
                }
            }

            foreach (var assignment in typeDecl.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                var left = assignment.Left.ToString();
                if (left.EndsWith(".Value"))
                {
                    var lineSpan = assignment.GetLocation().GetLineSpan();
                    var containingMethod = assignment.Ancestors()
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault()?.Identifier.Text ?? "<unknown>";

                    reactiveWrites.Add(new
                    {
                        target = left,
                        value = assignment.Right.ToString().Length > 80
                            ? assignment.Right.ToString()[..80] + "..."
                            : assignment.Right.ToString(),
                        containingMethod,
                        line = lineSpan.StartLinePosition.Line + 1
                    });
                }
            }

            foreach (var invocation in typeDecl.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var methodName2 = GetInvocationMethodName(invocation);
                if (methodName2 is "OnNext" or "OnCompleted" or "OnError" or "Execute")
                {
                    var lineSpan = invocation.GetLocation().GetLineSpan();
                    var containingMethod = invocation.Ancestors()
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault()?.Identifier.Text ?? "<unknown>";

                    reactiveWrites.Add(new
                    {
                        target = GetObservableSource(invocation) + $".{methodName2}()",
                        value = invocation.ArgumentList.Arguments.FirstOrDefault()?.ToString() ?? "",
                        containingMethod,
                        line = lineSpan.StartLinePosition.Line + 1
                    });
                }
            }
        }

        if (subscriptions.Count == 0 && reactiveFields.Count == 0)
            return $"No reactive patterns found in '{type.Name}'.";

        var (typePath, typeLine) = workspace.GetSymbolLocation(type);

        return ToonFormat.Toon.Encode(new
        {
            className = type.ToDisplayString(),
            filePath = typePath,
            reactiveFields = reactiveFields.Count > 0 ? reactiveFields : null,
            subscriptionCount = subscriptions.Count,
            subscriptions = subscriptions.Count > 0 ? subscriptions : null,
            potentialLeakCount = potentialLeaks.Count,
            potentialLeaks = potentialLeaks.Count > 0 ? potentialLeaks : null,
            reactiveWrites = reactiveWrites.Count > 0 ? reactiveWrites : null,
            summary = new
            {
                totalSubscriptions = subscriptions.Count,
                withAddTo = subscriptions.Count - potentialLeaks.Count,
                withoutAddTo = potentialLeaks.Count,
                reactiveFieldCount = reactiveFields.Count,
                writeCount = reactiveWrites.Count
            }
        });
    }

    [McpServerTool(Name = "get_reactive_chains"),
     Description("Analyze reactive subscriptions in a class: .Subscribe(), .AddTo(), Observable chains, " +
                 "ReactiveProperty/ReactiveEvent usage. Detects potential memory leaks (Subscribe without AddTo). " +
                 "Example: get_reactive_chains(className='CleaningSquadService')")]
    public static async Task<string> McpGetReactiveChains(
        WorkspaceService workspace,
        [Description("Class to analyze. Can be short name or fully-qualified.")]
        string className,
        [Description("If true, only show potential issues (Subscribe without AddTo). Default: false")]
        bool issuesOnly = false)
    {
        await workspace.EnsureLoadedAsync();
        return GetReactiveChains(workspace, className, issuesOnly);
    }

    private static bool IsReactiveType(ITypeSymbol type)
    {
        var name = type.Name;
        if (name.Contains("ReactiveProperty") || name.Contains("ReactiveEvent")
            || name.Contains("ReactiveCommand") || name.Contains("Subject")
            || name.Contains("ReadOnlyReactive"))
            return true;

        if (type.AllInterfaces.Any(i => i.Name == "IObservable" || i.Name == "ISubject"))
            return true;

        var current = type.BaseType;
        while (current != null)
        {
            if (current.Name.Contains("Subject") || current.Name.Contains("Observable"))
                return true;
            current = current.BaseType;
        }

        return false;
    }

    private static string GetObservableSource(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax ma)
        {
            var source = ma.Expression.ToString();
            return source.Length > 100 ? source[..100] + "..." : source;
        }
        return "<unknown>";
    }

    private static bool HasChainedMethod(InvocationExpressionSyntax invocation, string methodName)
    {
        if (invocation.Parent is MemberAccessExpressionSyntax parentMa
            && parentMa.Name.Identifier.Text == methodName)
            return true;

        var statement = invocation.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (statement != null)
        {
            return statement.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(inv =>
                {
                    if (inv.Expression is MemberAccessExpressionSyntax innerMa)
                        return innerMa.Name.Identifier.Text == methodName;
                    return false;
                });
        }

        return false;
    }
}
