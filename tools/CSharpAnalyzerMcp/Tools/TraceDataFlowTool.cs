using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class TraceDataFlowTool
{
    public static async Task<string> TraceDataFlow(
        WorkspaceService workspace,
        string className,
        string methodName,
        string? variableName = null,
        int? line = null,
        string depth = "local",
        int maxDepth = 2)
    {
        var types = workspace.FindTypesByName(className);
        if (types.Count == 0)
            return $"Type '{className}' not found.";

        var type = types[0];
        var methods = type.GetMembers(methodName).OfType<IMethodSymbol>().ToList();
        if (methods.Count == 0)
            return $"Method '{methodName}' not found in '{type.Name}'.";

        var method = methods[0];

        var methodLocation = method.Locations.FirstOrDefault(l => l.IsInSource);
        if (methodLocation == null)
            return $"No source code found for '{type.Name}.{methodName}'.";

        var tree = methodLocation.SourceTree!;
        var compilation = workspace.GetCompilationForTree(tree);
        var root = tree.GetRoot();
        var methodNode = root.FindNode(methodLocation.SourceSpan);
        var semanticModel = compilation.GetSemanticModel(tree);

        var methodDecl = methodNode.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault()
            ?? methodNode.AncestorsAndSelf().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault() as SyntaxNode;

        if (methodDecl == null)
            return $"Cannot find method body for '{type.Name}.{methodName}'.";

        var body = (methodDecl as MethodDeclarationSyntax)?.Body
            ?? (methodDecl as ConstructorDeclarationSyntax)?.Body;

        if (body == null)
        {
            var exprBody = (methodDecl as MethodDeclarationSyntax)?.ExpressionBody;
            if (exprBody != null)
            {
                return AnalyzeExpressionBody(workspace, method, exprBody, semanticModel, tree.FilePath);
            }
            return $"Method '{type.Name}.{methodName}' has no body to analyze.";
        }

        var dataFlow = semanticModel.AnalyzeDataFlow(body);
        if (dataFlow == null || !dataFlow.Succeeded)
            return $"Data flow analysis failed for '{type.Name}.{methodName}'.";

        var variableDetails = new List<object>();
        var targetVariables = GetTargetVariables(dataFlow, variableName, body, semanticModel, line);

        foreach (var variable in targetVariables)
        {
            var assignments = FindAssignments(variable, body, semanticModel);
            var origins = ClassifyOrigins(variable, method, body, semanticModel, compilation);

            variableDetails.Add(new
            {
                name = variable.Name,
                type = GetSymbolType(variable),
                assignments,
                origins
            });
        }

        var (methodPath, methodLine) = workspace.GetSymbolLocation(method);
        var localResult = new
        {
            method = new
            {
                name = method.Name,
                containingType = method.ContainingType?.Name,
                fullSignature = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                filePath = methodPath,
                line = methodLine
            },
            dataFlow = new
            {
                variablesReadInside = dataFlow.ReadInside.Select(s => s.Name).ToList(),
                variablesWrittenInside = dataFlow.WrittenInside.Select(s => s.Name).ToList(),
                dataFlowsIn = dataFlow.DataFlowsIn.Select(s => s.Name).ToList(),
                dataFlowsOut = dataFlow.DataFlowsOut.Select(s => s.Name).ToList()
            },
            variableDetails
        };

        if (depth == "cross_method" && maxDepth > 0)
        {
            var crossTrace = await TraceCrossMethod(workspace, method, targetVariables,
                body, semanticModel, compilation, maxDepth);

            return ToonFormat.Toon.Encode(new
            {
                localResult.method,
                localResult.dataFlow,
                localResult.variableDetails,
                crossMethodTrace = crossTrace
            });
        }

        return ToonFormat.Toon.Encode(localResult);
    }

    [McpServerTool(Name = "trace_data_flow"),
     Description("Trace the origin of a variable's value through assignments, parameters, field accesses, and method returns. " +
                 "Answers: 'Where does the value of result on line 45 come from?' " +
                 "Supports local (intra-method) and cross-method tracing. " +
                 "Example: trace_data_flow(className='PricingService', methodName='CalculateTotal', variableName='result')")]
    public static async Task<string> McpTraceDataFlow(
        WorkspaceService workspace,
        [Description("Class containing the method to analyze.")]
        string className,
        [Description("Method to analyze.")]
        string methodName,
        [Description("Specific variable to trace. If omitted, analyzes all variables in the method.")]
        string? variableName = null,
        [Description("Line number to narrow context (disambiguate reused variable names).")]
        int? line = null,
        [Description("'local' — within method only (default), 'cross_method' — trace through calls.")]
        string depth = "local",
        [Description("Max traversal depth for cross_method. Default: 2, max: 5.")]
        int maxDepth = 2)
    {
        await workspace.EnsureLoadedAsync();
        return await TraceDataFlow(workspace, className, methodName, variableName, line, depth, Math.Min(maxDepth, 5));
    }

    private static List<ISymbol> GetTargetVariables(
        DataFlowAnalysis dataFlow, string? variableName,
        BlockSyntax body, SemanticModel model, int? line)
    {
        var allVariables = dataFlow.WrittenInside
            .Concat(dataFlow.ReadInside)
            .Distinct<ISymbol>(SymbolEqualityComparer.Default)
            .Where(s => !s.IsImplicitlyDeclared)
            .ToList();

        if (variableName != null)
        {
            var filtered = allVariables.Where(s => s.Name == variableName).ToList();
            if (filtered.Count == 0)
            {
                filtered = dataFlow.DataFlowsIn
                    .Where(s => s.Name == variableName)
                    .ToList();
            }

            if (line.HasValue && filtered.Count > 1)
            {
                var lineFiltered = filtered.Where(s =>
                {
                    var loc = s.Locations.FirstOrDefault(l => l.IsInSource);
                    if (loc == null) return false;
                    return loc.GetLineSpan().StartLinePosition.Line + 1 == line.Value;
                }).ToList();

                if (lineFiltered.Count > 0)
                    return lineFiltered;
            }

            return filtered;
        }

        return dataFlow.WrittenInside
            .Where(s => !s.IsImplicitlyDeclared && s.Name != "this")
            .ToList();
    }

    private static List<object> FindAssignments(ISymbol variable, BlockSyntax body, SemanticModel model)
    {
        var assignments = new List<object>();

        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            var leftSymbol = model.GetSymbolInfo(assignment.Left).Symbol;
            if (leftSymbol != null && SymbolEqualityComparer.Default.Equals(leftSymbol, variable))
            {
                var lineSpan = assignment.GetLocation().GetLineSpan();
                var rhs = assignment.Right.ToString();
                if (rhs.Length > 120) rhs = rhs[..120] + "...";

                var deps = new List<string>();
                foreach (var identifier in assignment.Right.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    var identSymbol = model.GetSymbolInfo(identifier).Symbol;
                    if (identSymbol is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)
                    {
                        if (!deps.Contains(identSymbol.Name))
                            deps.Add(identSymbol.Name);
                    }
                }

                assignments.Add(new
                {
                    line = lineSpan.StartLinePosition.Line + 1,
                    expression = rhs,
                    dependsOn = deps
                });
            }
        }

        foreach (var varDecl in body.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            var declSymbol = model.GetDeclaredSymbol(varDecl);
            if (declSymbol != null && SymbolEqualityComparer.Default.Equals(declSymbol, variable)
                && varDecl.Initializer != null)
            {
                var lineSpan = varDecl.GetLocation().GetLineSpan();
                var rhs = varDecl.Initializer.Value.ToString();
                if (rhs.Length > 120) rhs = rhs[..120] + "...";

                var deps = new List<string>();
                foreach (var identifier in varDecl.Initializer.Value.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    var identSymbol = model.GetSymbolInfo(identifier).Symbol;
                    if (identSymbol is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)
                    {
                        if (!deps.Contains(identSymbol.Name))
                            deps.Add(identSymbol.Name);
                    }
                }

                assignments.Add(new
                {
                    line = lineSpan.StartLinePosition.Line + 1,
                    expression = rhs,
                    dependsOn = deps
                });
            }
        }

        return assignments.OrderBy(a => ((dynamic)a).line).ToList();
    }

    private static List<object> ClassifyOrigins(
        ISymbol variable, IMethodSymbol method,
        BlockSyntax body, SemanticModel model, Compilation compilation)
    {
        var origins = new List<object>();

        var param = method.Parameters.FirstOrDefault(p => p.Name == variable.Name);
        if (param != null)
        {
            origins.Add(new
            {
                source = "parameter",
                paramIndex = method.Parameters.IndexOf(param),
                type = param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            });
            return origins;
        }

        if (variable is IFieldSymbol field && SymbolEqualityComparer.Default.Equals(field.ContainingType, method.ContainingType))
        {
            origins.Add(new
            {
                source = "field",
                containingType = field.ContainingType?.Name,
                type = field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            });
            return origins;
        }

        if (variable is IPropertySymbol prop && SymbolEqualityComparer.Default.Equals(prop.ContainingType, method.ContainingType))
        {
            origins.Add(new
            {
                source = "property",
                containingType = prop.ContainingType?.Name,
                type = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            });
            return origins;
        }

        if (variable is ILocalSymbol)
        {
            foreach (var varDecl in body.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                var declSymbol = model.GetDeclaredSymbol(varDecl);
                if (declSymbol != null && SymbolEqualityComparer.Default.Equals(declSymbol, variable)
                    && varDecl.Initializer != null)
                {
                    var initExpr = varDecl.Initializer.Value;

                    if (initExpr is InvocationExpressionSyntax invocation)
                    {
                        var calledSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        if (calledSymbol != null)
                        {
                            origins.Add(new
                            {
                                source = "method_return",
                                method = calledSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                containingType = calledSymbol.ContainingType?.Name,
                                returnType = calledSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                            });
                            continue;
                        }
                    }

                    if (initExpr is AwaitExpressionSyntax awaitExpr
                        && awaitExpr.Expression is InvocationExpressionSyntax awaitedInvocation)
                    {
                        var calledSymbol = model.GetSymbolInfo(awaitedInvocation).Symbol as IMethodSymbol;
                        if (calledSymbol != null)
                        {
                            origins.Add(new
                            {
                                source = "awaited_method_return",
                                method = calledSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                containingType = calledSymbol.ContainingType?.Name,
                                returnType = calledSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                            });
                            continue;
                        }
                    }

                    origins.Add(new
                    {
                        source = "local_assignment",
                        expression = initExpr.ToString().Length > 100
                            ? initExpr.ToString()[..100] + "..."
                            : initExpr.ToString()
                    });
                }
            }
        }

        if (origins.Count == 0)
        {
            origins.Add(new { source = "unknown" });
        }

        return origins;
    }

    private static async Task<List<object>> TraceCrossMethod(
        WorkspaceService workspace,
        IMethodSymbol method,
        List<ISymbol> variables,
        BlockSyntax body,
        SemanticModel model,
        Compilation compilation,
        int maxDepth,
        int currentDepth = 0,
        HashSet<ISymbol>? visited = null)
    {
        visited ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (!visited.Add(method)) return [];
        if (currentDepth >= maxDepth) return [];

        var traces = new List<object>();
        var solution = workspace.Project.Solution;

        foreach (var variable in variables)
        {
            if (variable is IFieldSymbol or IPropertySymbol)
            {
                var fieldName = variable.Name;
                var containingTypeName = variable.ContainingType?.Name ?? method.ContainingType?.Name;

                if (containingTypeName != null)
                {
                    var mutations = await TraceFieldMutationsTool.TraceFieldMutations(
                        workspace, containingTypeName, fieldName, "write", 10);

                    traces.Add(new
                    {
                        variable = fieldName,
                        source = "field",
                        containingType = containingTypeName,
                        mutations
                    });
                }
            }

            if (variable is IParameterSymbol param)
            {
                var callerRefs = await SymbolFinder.FindCallersAsync(method, solution);
                var callerDetails = new List<object>();

                foreach (var caller in callerRefs.Where(c => c.IsDirect).Take(10))
                {
                    var callerMethod = caller.CallingSymbol as IMethodSymbol;
                    if (callerMethod == null) continue;

                    var passedExpr = FindPassedExpression(callerMethod, method, param, compilation);
                    var (callerPath, callerLine) = workspace.GetSymbolLocation(callerMethod);

                    callerDetails.Add(new
                    {
                        caller = callerMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        filePath = callerPath,
                        line = callerLine,
                        passedExpression = passedExpr
                    });
                }

                traces.Add(new
                {
                    variable = param.Name,
                    source = "parameter",
                    paramIndex = method.Parameters.IndexOf(param),
                    callers = callerDetails
                });
            }
        }

        return traces;
    }

    private static string? FindPassedExpression(
        IMethodSymbol callerMethod, IMethodSymbol calledMethod,
        IParameterSymbol param, Compilation compilation)
    {
        var paramIndex = calledMethod.Parameters.IndexOf(param);
        if (paramIndex < 0) return null;

        foreach (var location in callerMethod.Locations.Where(l => l.IsInSource))
        {
            var tree = location.SourceTree;
            if (tree == null) continue;

            var root = tree.GetRoot();
            var callerNode = root.FindNode(location.SourceSpan);
            var semanticModel = compilation.GetSemanticModel(tree);

            foreach (var invocation in callerNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (invokedSymbol == null) continue;

                if (!SymbolEqualityComparer.Default.Equals(invokedSymbol.OriginalDefinition, calledMethod.OriginalDefinition))
                    continue;

                var args = invocation.ArgumentList.Arguments;

                foreach (var arg in args)
                {
                    if (arg.NameColon != null && arg.NameColon.Name.Identifier.Text == param.Name)
                    {
                        var expr = arg.Expression.ToString();
                        return expr.Length > 100 ? expr[..100] + "..." : expr;
                    }
                }

                if (paramIndex < args.Count && args[paramIndex].NameColon == null)
                {
                    var expr = args[paramIndex].Expression.ToString();
                    return expr.Length > 100 ? expr[..100] + "..." : expr;
                }
            }
        }

        return null;
    }

    private static string AnalyzeExpressionBody(
        WorkspaceService workspace, IMethodSymbol method,
        ArrowExpressionClauseSyntax exprBody, SemanticModel model, string filePath)
    {
        var (methodPath, methodLine) = workspace.GetSymbolLocation(method);

        var expr = exprBody.Expression.ToString();
        if (expr.Length > 200) expr = expr[..200] + "...";

        var deps = new List<string>();
        foreach (var identifier in exprBody.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var identSymbol = model.GetSymbolInfo(identifier).Symbol;
            if (identSymbol is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)
            {
                if (!deps.Contains(identSymbol.Name))
                    deps.Add(identSymbol.Name);
            }
        }

        return ToonFormat.Toon.Encode(new
        {
            method = new
            {
                name = method.Name,
                containingType = method.ContainingType?.Name,
                filePath = methodPath,
                line = methodLine
            },
            expressionBody = new
            {
                expression = expr,
                dependsOn = deps
            }
        });
    }

}
