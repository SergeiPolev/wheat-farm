using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;
using ModelContextProtocol.Server;
using static CSharpAnalyzerMcp.Services.SyntaxHelper;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class TraceFieldMutationsTool
{
    public static async Task<string> TraceFieldMutations(
        WorkspaceService workspace,
        string className,
        string fieldName,
        string mutationType = "write",
        int maxResults = 30)
    {
        var symbols = workspace.FindSymbolsByName(fieldName, "field", className);
        if (symbols.Count == 0)
        {
            symbols = workspace.FindSymbolsByName(fieldName, "property", className);
        }

        if (symbols.Count == 0)
            return $"Field or property '{fieldName}' not found in '{className}'. Try get_class_info to see available members.";

        if (symbols.Count > 10)
        {
            var options = symbols.Take(20).Select(s => new
            {
                name = s.Name,
                kind = s.Kind.ToString().ToLowerInvariant(),
                containingType = s.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "",
                display = s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            }).ToList();

            return ToonFormat.Toon.Encode(new
            {
                error = "too_many_matches",
                message = $"Found {symbols.Count} symbols matching '{fieldName}' in '{className}'. Narrow down by checking get_class_info first.",
                matches = options
            });
        }

        var symbol = symbols[0];
        var solution = workspace.Project.Solution;
        var symbolTree = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree;
        var compilation = symbolTree != null ? workspace.GetCompilationForTree(symbolTree) : workspace.Compilation;

        var references = await SymbolFinder.FindReferencesAsync(symbol, solution);

        var writes = new List<object>();
        var reads = new List<object>();
        var treeCache = new Dictionary<SyntaxTree, (SyntaxNode Root, SemanticModel Model)>();

        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                if (writes.Count + reads.Count >= maxResults) break;

                var syntaxTree = location.Location.SourceTree;
                if (syntaxTree == null) continue;

                if (!treeCache.TryGetValue(syntaxTree, out var cached))
                {
                    cached = (syntaxTree.GetRoot(), workspace.GetCompilationForTree(syntaxTree).GetSemanticModel(syntaxTree));
                    treeCache[syntaxTree] = cached;
                }

                var node = cached.Root.FindNode(location.Location.SourceSpan);
                var semanticModel = cached.Model;

                var lineSpan = location.Location.GetLineSpan();
                var filePath = lineSpan.Path;
                var line = lineSpan.StartLinePosition.Line + 1;

                var writeInfo = ClassifyWrite(node, semanticModel);

                var containingMember = GetContainingMember(node, semanticModel);
                var lineText = workspace.GetLineText(filePath, line);

                if (writeInfo != null)
                {
                    writes.Add(new
                    {
                        filePath,
                        line,
                        containingMember,
                        expression = writeInfo.Value.Expression,
                        writeKind = writeInfo.Value.Kind,
                        lineText
                    });
                }
                else
                {
                    reads.Add(new
                    {
                        filePath,
                        line,
                        containingMember,
                        lineText
                    });
                }
            }
        }

        var (symPath, symLine) = workspace.GetSymbolLocation(symbol);
        var symbolInfo = new
        {
            name = symbol.Name,
            kind = symbol.Kind.ToString().ToLowerInvariant(),
            type = GetSymbolType(symbol),
            containingType = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            filePath = symPath,
            line = symLine
        };

        return mutationType switch
        {
            "write" => ToonFormat.Toon.Encode(new
            {
                symbol = symbolInfo,
                writeCount = writes.Count,
                writes
            }),
            "read" => ToonFormat.Toon.Encode(new
            {
                symbol = symbolInfo,
                readCount = reads.Count,
                reads
            }),
            _ => ToonFormat.Toon.Encode(new
            {
                symbol = symbolInfo,
                writeCount = writes.Count,
                writes,
                readCount = reads.Count,
                reads
            })
        };
    }

    [McpServerTool(Name = "trace_field_mutations"),
     Description("Trace where a field or property is written (mutated) vs read. " +
                 "Unlike find_usages which returns all references mixed together, this tool separates " +
                 "writes (assignments, +=, ref/out) from reads. Essential for bug investigation: " +
                 "'where does _price get its wrong value?' " +
                 "Example: trace_field_mutations(className='HotelRoom', fieldName='_price')")]
    public static async Task<string> McpTraceFieldMutations(
        WorkspaceService workspace,
        [Description("Class containing the field/property.")]
        string className,
        [Description("Field or property name. If field not found, falls back to property.")]
        string fieldName,
        [Description("'write' — only mutations, 'read' — only reads, 'both' — separate lists. Default: 'write'")]
        string mutationType = "write",
        [Description("Maximum results per category. Default: 30")]
        int maxResults = 30)
    {
        await workspace.EnsureLoadedAsync();
        return await TraceFieldMutations(workspace, className, fieldName, mutationType, maxResults);
    }

    private static (string Kind, string Expression)? ClassifyWrite(SyntaxNode node, SemanticModel model)
    {
        var operation = model.GetOperation(node);
        if (operation == null)
            return ClassifyWriteFallback(node);

        var parent = operation.Parent;
        while (parent is IConversionOperation or IParenthesizedOperation)
            parent = parent.Parent;

        if (parent is ISimpleAssignmentOperation simpleAssign
            && simpleAssign.Target.Syntax.Span.Contains(node.Span))
        {
            var rhs = simpleAssign.Value.Syntax.ToString();
            if (rhs.Length > 120) rhs = rhs[..120] + "...";
            return ("simple_assignment", rhs);
        }

        if (parent is ICompoundAssignmentOperation compoundAssign
            && compoundAssign.Target.Syntax.Span.Contains(node.Span))
        {
            var kind = compoundAssign.OperatorKind switch
            {
                BinaryOperatorKind.Add => "add_assignment",
                BinaryOperatorKind.Subtract => "subtract_assignment",
                BinaryOperatorKind.Multiply => "multiply_assignment",
                BinaryOperatorKind.Divide => "divide_assignment",
                _ => "compound_assignment"
            };
            var rhs = compoundAssign.Value.Syntax.ToString();
            if (rhs.Length > 120) rhs = rhs[..120] + "...";
            return (kind, rhs);
        }

        if (parent is ICoalesceAssignmentOperation coalesceAssign
            && coalesceAssign.Target.Syntax.Span.Contains(node.Span))
        {
            var rhs = coalesceAssign.Value.Syntax.ToString();
            if (rhs.Length > 120) rhs = rhs[..120] + "...";
            return ("coalesce_assignment", rhs);
        }

        if (parent is IIncrementOrDecrementOperation incDec
            && incDec.Target.Syntax.Span.Contains(node.Span))
        {
            return ("increment_decrement", incDec.Syntax.ToString());
        }

        if (parent is IArgumentOperation argOp)
        {
            if (argOp.Parameter?.RefKind == RefKind.Ref)
                return ("ref_argument", $"passed as ref to {GetCallNameFromOperation(argOp)}");
            if (argOp.Parameter?.RefKind == RefKind.Out)
                return ("out_argument", $"passed as out to {GetCallNameFromOperation(argOp)}");
        }

        return null;
    }

    private static (string Kind, string Expression)? ClassifyWriteFallback(SyntaxNode node)
    {
        var current = node;
        while (current != null)
        {
            if (current.Parent is AssignmentExpressionSyntax assignment && assignment.Left.Span.Contains(node.Span))
            {
                var rhs = assignment.Right.ToString();
                if (rhs.Length > 120) rhs = rhs[..120] + "...";
                return ("simple_assignment", rhs);
            }

            if (current is StatementSyntax) break;
            current = current.Parent;
        }

        return null;
    }

    private static string GetCallNameFromOperation(IArgumentOperation argOp)
    {
        if (argOp.Parent is IInvocationOperation invocationOp)
            return invocationOp.TargetMethod.Name;
        if (argOp.Parent is IObjectCreationOperation creationOp)
            return creationOp.Type?.Name ?? "<constructor>";
        return "<unknown>";
    }

}
