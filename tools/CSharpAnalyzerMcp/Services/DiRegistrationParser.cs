using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpAnalyzerMcp.Services;

public sealed class DiRegistrationParser
{
    private readonly Compilation _compilation;

    public DiRegistrationParser(Compilation compilation)
    {
        _compilation = compilation;
    }

    public List<DiRegistration> ParseInstaller(INamedTypeSymbol installerType)
    {
        var results = new List<DiRegistration>();

        foreach (var location in installerType.Locations.Where(l => l.IsInSource))
        {
            var tree = location.SourceTree;
            if (tree == null) continue;

            var root = tree.GetRoot();
            var semanticModel = _compilation.GetSemanticModel(tree);

            var typeDecl = root.FindNode(location.SourceSpan)
                .AncestorsAndSelf()
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault();

            if (typeDecl == null) continue;

            var entryMethods = new[] { "InstallFeature", "InstallShared", "Install", "Configure" };
            var parsedMethods = new HashSet<string>();

            foreach (var method in typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var methodName = method.Identifier.Text;
                if (!entryMethods.Contains(methodName)) continue;
                if (!parsedMethods.Add(methodName)) continue;

                var section = DetermineSection(methodName);

                if (method.Body != null)
                {
                    ParseMethodBodyRecursive(method.Body, semanticModel, section, tree.FilePath,
                        results, typeDecl, parsedMethods);
                }
            }
        }

        return results;
    }

    public List<DiRegistration> ParseAllInstallers(string? filterTypeName = null)
    {
        var results = new List<DiRegistration>();

        foreach (var tree in _compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var semanticModel = _compilation.GetSemanticModel(tree);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (typeSymbol == null) continue;

                var isInstaller = typeSymbol.AllInterfaces.Any(i => i.Name == "IFeatureInstaller")
                    || typeSymbol.Name.EndsWith("Installer")
                    || typeSymbol.Name.EndsWith("Scope");

                if (!isInstaller) continue;

                var entryMethods = new[] { "InstallFeature", "InstallShared", "Install", "Configure" };
                var parsedMethods = new HashSet<string>();

                foreach (var method in typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    var methodName = method.Identifier.Text;
                    if (!entryMethods.Contains(methodName)) continue;
                    if (!parsedMethods.Add(methodName)) continue;

                    var section = DetermineSection(methodName);

                    if (method.Body != null)
                    {
                        ParseMethodBodyRecursive(method.Body, semanticModel, section, tree.FilePath,
                            results, typeDecl, parsedMethods);
                    }
                }
            }
        }

        if (filterTypeName != null)
        {
            results = results.Where(r =>
                r.RegisteredType?.Contains(filterTypeName, StringComparison.OrdinalIgnoreCase) == true
                || r.InterfaceType?.Contains(filterTypeName, StringComparison.OrdinalIgnoreCase) == true
                || r.RegisteredAs.Any(a => a.Contains(filterTypeName, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        return results;
    }

    private void ParseMethodBodyRecursive(BlockSyntax body, SemanticModel model, string section,
        string filePath, List<DiRegistration> results, TypeDeclarationSyntax typeDecl,
        HashSet<string> parsedMethods)
    {
        var useEntryPointsCalls = new HashSet<InvocationExpressionSyntax>();

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsUseEntryPointsCall(invocation))
            {
                useEntryPointsCalls.Add(invocation);
                ParseUseEntryPoints(invocation, model, section, filePath, results);
            }
        }

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (useEntryPointsCalls.Any(ue => ue.Span.Contains(invocation.Span) && ue != invocation))
                continue;

            var reg = TryParseRegistration(invocation, model, section, filePath);
            if (reg != null)
            {
                results.Add(reg);
                continue;
            }

            var symbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol != null && parsedMethods.Add(symbol.Name))
            {
                var helperMethod = typeDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == symbol.Name);

                if (helperMethod?.Body != null)
                {
                    ParseMethodBodyRecursive(helperMethod.Body, model, section, filePath,
                        results, typeDecl, parsedMethods);
                }
            }
        }
    }

    private DiRegistration? TryParseRegistration(InvocationExpressionSyntax invocation,
        SemanticModel model, string section, string filePath)
    {
        var methodName = GetMethodName(invocation);
        if (methodName == null) return null;

        var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        switch (methodName)
        {
            case "Register" when HasGenericArgs(invocation):
                return ParseRegisterGeneric(invocation, model, section, filePath, line);

            case "Register" when !HasGenericArgs(invocation):
                return ParseRegisterFactory(invocation, model, section, filePath, line);

            case "RegisterInstance":
                return ParseRegisterInstance(invocation, model, section, filePath, line);

            case "RegisterEntryPoint":
                return ParseRegisterEntryPoint(invocation, model, section, filePath, line);

            default:
                return null;
        }
    }

    private DiRegistration? ParseRegisterGeneric(InvocationExpressionSyntax invocation,
        SemanticModel model, string section, string filePath, int line)
    {
        var typeArgs = GetGenericTypeArgs(invocation);
        if (typeArgs.Count == 0) return null;

        var lifetime = ExtractLifetime(invocation, model);
        var chainMethods = GetChainedMethods(invocation);

        string registeredType;
        string? interfaceType = null;

        if (typeArgs.Count >= 2)
        {
            interfaceType = typeArgs[0];
            registeredType = typeArgs[1];
        }
        else
        {
            registeredType = typeArgs[0];
        }

        var registeredAs = BuildRegisteredAs(chainMethods, interfaceType);

        return new DiRegistration
        {
            RegistrationKind = "Register",
            RegisteredType = registeredType,
            InterfaceType = interfaceType,
            Lifetime = lifetime ?? "Singleton",
            Section = section,
            RegisteredAs = registeredAs,
            FilePath = filePath,
            Line = line
        };
    }

    private DiRegistration? ParseRegisterFactory(InvocationExpressionSyntax invocation,
        SemanticModel model, string section, string filePath, int line)
    {
        var lifetime = ExtractLifetime(invocation, model);
        var chainMethods = GetChainedMethods(invocation);

        string registeredType = "<factory>";
        var symbolInfo = model.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol ms && ms.ReturnType is INamedTypeSymbol returnType)
        {
            var args = invocation.ArgumentList.Arguments;
            if (args.Count > 0)
            {
                var firstArg = args[0].Expression;
                if (firstArg is SimpleLambdaExpressionSyntax lambda)
                {
                    if (lambda.Body is ObjectCreationExpressionSyntax creation)
                    {
                        var typeInfo = model.GetTypeInfo(creation);
                        registeredType = typeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "<unknown>";
                    }
                }
                else if (firstArg is IdentifierNameSyntax identifier)
                {
                    var methodSymbol = model.GetSymbolInfo(identifier).Symbol as IMethodSymbol;
                    if (methodSymbol != null)
                    {
                        registeredType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }
                }
            }
        }

        var registeredAs = BuildRegisteredAs(chainMethods, null);

        return new DiRegistration
        {
            RegistrationKind = "RegisterFactory",
            RegisteredType = registeredType,
            Lifetime = lifetime ?? "Transient",
            Section = section,
            RegisteredAs = registeredAs,
            FilePath = filePath,
            Line = line
        };
    }

    private DiRegistration? ParseRegisterInstance(InvocationExpressionSyntax invocation,
        SemanticModel model, string section, string filePath, int line)
    {
        var chainMethods = GetChainedMethods(invocation);

        string registeredType = "<instance>";
        var args = invocation.ArgumentList.Arguments;
        if (args.Count > 0)
        {
            var typeInfo = model.GetTypeInfo(args[0].Expression);
            if (typeInfo.Type != null)
            {
                registeredType = typeInfo.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }
        }

        var registeredAs = BuildRegisteredAs(chainMethods, null);

        return new DiRegistration
        {
            RegistrationKind = "RegisterInstance",
            RegisteredType = registeredType,
            Lifetime = "Singleton",
            Section = section,
            RegisteredAs = registeredAs,
            FilePath = filePath,
            Line = line
        };
    }

    private DiRegistration? ParseRegisterEntryPoint(InvocationExpressionSyntax invocation,
        SemanticModel model, string section, string filePath, int line)
    {
        var typeArgs = GetGenericTypeArgs(invocation);
        if (typeArgs.Count == 0) return null;

        var lifetime = ExtractLifetime(invocation, model);
        var chainMethods = GetChainedMethods(invocation);
        var registeredAs = BuildRegisteredAs(chainMethods, null);

        return new DiRegistration
        {
            RegistrationKind = "RegisterEntryPoint",
            RegisteredType = typeArgs[0],
            Lifetime = lifetime ?? "Singleton",
            Section = section,
            RegisteredAs = registeredAs,
            IsEntryPoint = true,
            FilePath = filePath,
            Line = line
        };
    }

    private void ParseUseEntryPoints(InvocationExpressionSyntax invocation,
        SemanticModel model, string section, string filePath, List<DiRegistration> results)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return;

        var lambdaArg = args[0].Expression;
        IEnumerable<InvocationExpressionSyntax>? addCalls = null;

        if (lambdaArg is SimpleLambdaExpressionSyntax simpleLambda)
        {
            addCalls = (simpleLambda.Body as BlockSyntax)?.DescendantNodes().OfType<InvocationExpressionSyntax>()
                ?? (simpleLambda.Body as InvocationExpressionSyntax != null
                    ? new[] { (InvocationExpressionSyntax)simpleLambda.Body }
                    : null);
        }
        else if (lambdaArg is ParenthesizedLambdaExpressionSyntax parenLambda)
        {
            addCalls = (parenLambda.Body as BlockSyntax)?.DescendantNodes().OfType<InvocationExpressionSyntax>();
        }

        if (addCalls == null) return;

        var addCallsList = addCalls.ToList();
        var outerCalls = addCallsList.Where(call =>
            !addCallsList.Any(other =>
                other != call
                && other.Span.Contains(call.Span))).ToList();

        foreach (var addCall in outerCalls)
        {
            var addMethodName = GetRootMethodName(addCall);
            if (addMethodName != "Add") continue;

            var rootInvocation = GetRootInvocation(addCall);
            var typeArgs = GetGenericTypeArgs(rootInvocation);
            if (typeArgs.Count == 0) continue;

            var chainMethods = GetChainedMethodsFromRoot(addCall, rootInvocation);
            var registeredAs = BuildRegisteredAs(chainMethods, null);
            var line = addCall.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            results.Add(new DiRegistration
            {
                RegistrationKind = "UseEntryPoints.Add",
                RegisteredType = typeArgs[0],
                Lifetime = "Singleton",
                Section = section,
                RegisteredAs = registeredAs,
                IsEntryPoint = true,
                FilePath = filePath,
                Line = line
            });
        }
    }

    private static bool IsUseEntryPointsCall(InvocationExpressionSyntax invocation)
    {
        var name = GetMethodName(invocation);
        return name == "UseEntryPoints";
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name switch
            {
                GenericNameSyntax gn => gn.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => null
            },
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax gn => gn.Identifier.Text,
            _ => null
        };
    }

    private static string? GetRootMethodName(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (true)
        {
            if (current.Expression is MemberAccessExpressionSyntax ma
                && ma.Expression is InvocationExpressionSyntax inner)
            {
                current = inner;
                continue;
            }
            break;
        }
        return GetMethodName(current);
    }

    private static InvocationExpressionSyntax GetRootInvocation(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (true)
        {
            if (current.Expression is MemberAccessExpressionSyntax ma
                && ma.Expression is InvocationExpressionSyntax inner)
            {
                current = inner;
                continue;
            }
            break;
        }
        return current;
    }

    private static bool HasGenericArgs(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name is GenericNameSyntax,
            GenericNameSyntax => true,
            _ => false
        };
    }

    private static List<string> GetGenericTypeArgs(InvocationExpressionSyntax invocation)
    {
        TypeArgumentListSyntax? typeArgList = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma when ma.Name is GenericNameSyntax gn => gn.TypeArgumentList,
            GenericNameSyntax gn => gn.TypeArgumentList,
            _ => null
        };

        if (typeArgList == null) return [];

        return typeArgList.Arguments
            .Select(a => a.ToString())
            .ToList();
    }

    private static string? ExtractLifetime(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var constantValue = model.GetConstantValue(arg.Expression);
            if (constantValue.HasValue && constantValue.Value is int intVal)
            {
                return intVal switch
                {
                    0 => "Singleton",
                    1 => "Transient",
                    2 => "Scoped",
                    _ => intVal.ToString()
                };
            }

            var typeInfo = model.GetTypeInfo(arg.Expression);
            if (typeInfo.Type?.Name == "Lifetime")
            {
                var symbol = model.GetSymbolInfo(arg.Expression).Symbol;
                if (symbol is IFieldSymbol field)
                    return field.Name;
            }

            var text = arg.Expression.ToString();
            if (text.Contains("Lifetime.Singleton")) return "Singleton";
            if (text.Contains("Lifetime.Transient")) return "Transient";
            if (text.Contains("Lifetime.Scoped")) return "Scoped";
        }
        return null;
    }

    private static List<(string Name, List<string> TypeArgs)> GetChainedMethods(InvocationExpressionSyntax invocation)
    {
        var methods = new List<(string Name, List<string> TypeArgs)>();
        SyntaxNode current = invocation;

        while (current.Parent is MemberAccessExpressionSyntax parentMa
            && parentMa.Parent is InvocationExpressionSyntax parentInvocation)
        {
            var name = parentMa.Name is GenericNameSyntax gn ? gn.Identifier.Text : parentMa.Name.ToString();
            var typeArgs = parentMa.Name is GenericNameSyntax gn2
                ? gn2.TypeArgumentList.Arguments.Select(a => a.ToString()).ToList()
                : new List<string>();
            methods.Add((name, typeArgs));
            current = parentInvocation;
        }

        return methods;
    }

    private static List<(string Name, List<string> TypeArgs)> GetChainedMethodsFromRoot(InvocationExpressionSyntax outermost,
        InvocationExpressionSyntax root)
    {
        var methods = new List<(string Name, List<string> TypeArgs)>();
        SyntaxNode current = root;

        while (current.Parent is MemberAccessExpressionSyntax parentMa
            && parentMa.Parent is InvocationExpressionSyntax parentInvocation)
        {
            var name = parentMa.Name is GenericNameSyntax gn ? gn.Identifier.Text : parentMa.Name.ToString();
            var typeArgs = parentMa.Name is GenericNameSyntax gn2
                ? gn2.TypeArgumentList.Arguments.Select(a => a.ToString()).ToList()
                : new List<string>();
            methods.Add((name, typeArgs));
            current = parentInvocation;

            if (parentInvocation == outermost) break;
        }

        return methods;
    }

    private static List<string> BuildRegisteredAs(List<(string Name, List<string> TypeArgs)> chainMethods, string? interfaceType)
    {
        var registeredAs = new List<string>();

        if (interfaceType != null)
        {
            registeredAs.Add(interfaceType);
        }

        foreach (var (name, typeArgs) in chainMethods)
        {
            if (name == "AsSelf")
                registeredAs.Add("self");
            else if (name == "AsImplementedInterfaces")
                registeredAs.Add("all-interfaces");
            else if (name.StartsWith("As") && typeArgs.Count > 0)
                registeredAs.AddRange(typeArgs);
            else if (name.StartsWith("As<"))
                registeredAs.Add(name[3..^1]);
        }

        return registeredAs;
    }

    private static string DetermineSection(string methodName)
    {
        return methodName switch
        {
            "InstallFeature" => "feature",
            "InstallShared" => "shared",
            "Install" => "install",
            "Configure" => "configure",
            _ => "other"
        };
    }
}

public record DiRegistration
{
    public string RegistrationKind { get; init; } = "";
    public string? RegisteredType { get; init; }
    public string? InterfaceType { get; init; }
    public string Lifetime { get; init; } = "Singleton";
    public string Section { get; init; } = "";
    public List<string> RegisteredAs { get; init; } = [];
    public bool IsEntryPoint { get; init; }
    public string? FilePath { get; init; }
    public int Line { get; init; }
}
