using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace CSharpAnalyzerMcp.Services;

public sealed class WorkspaceService : IDisposable
{
    private readonly ILogger<WorkspaceService> _logger;
    private readonly TaskCompletionSource _ready = new();
    private MSBuildWorkspace? _workspace;
    private Project? _project;
    private Compilation? _compilation;
    private string? _loadedPath;

    private Dictionary<string, Compilation> _allCompilations = new();

    public WorkspaceService(ILogger<WorkspaceService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(string projectPath, CancellationToken ct = default)
    {
        try
        {
            await LoadProjectAsync(projectPath, ct);
            _ready.TrySetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project: {Path}", projectPath);
            _ready.TrySetException(ex);
        }
    }

    public async Task EnsureLoadedAsync()
    {
        try
        {
            await _ready.Task;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Project failed to load: {ex.Message}. Check logs for details.", ex);
        }
    }

    public Compilation Compilation => _compilation
        ?? throw new InvalidOperationException("Project not loaded. Call LoadProjectAsync first.");

    public Project Project => _project
        ?? throw new InvalidOperationException("Project not loaded. Call LoadProjectAsync first.");

    public IReadOnlyDictionary<string, Compilation> AllCompilations => _allCompilations;

    public async Task LoadProjectAsync(string projectPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Loading project: {Path}", projectPath);

        var fullPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Project file not found: {fullPath}");

        _loadedPath = fullPath;

        _workspace = MSBuildWorkspace.Create();
        _workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                _logger.LogError("Workspace failure: {Message}", e.Diagnostic.Message);
            else
                _logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message);
        });

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension == ".sln")
        {
            await LoadSolutionAsync(fullPath, ct);
        }
        else
        {
            _project = await _workspace.OpenProjectAsync(fullPath, cancellationToken: ct);
            _compilation = await _project.GetCompilationAsync(ct)
                ?? throw new InvalidOperationException("Failed to get compilation");
            _allCompilations[_project.Name] = _compilation;
        }

        var totalTypes = 0;
        var totalErrors = 0;
        var totalWarnings = 0;

        foreach (var (name, comp) in _allCompilations)
        {
            var diagnostics = comp.GetDiagnostics(ct);
            var errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
            var types = comp.GetSymbolsWithName(_ => true, SymbolFilter.Type).Count();
            totalTypes += types;
            totalErrors += errors;
            totalWarnings += warnings;

            if (errors > 0)
            {
                _logger.LogDebug("  {Project}: {Types} types, {Errors} errors", name, types, errors);
            }
        }

        _logger.LogInformation(
            "Solution loaded: {ProjectCount} projects, {TypeCount} types, {Errors} errors, {Warnings} warnings",
            _allCompilations.Count, totalTypes, totalErrors, totalWarnings);
    }

    private async Task LoadSolutionAsync(string solutionPath, CancellationToken ct)
    {
        _logger.LogInformation("Opening solution: {Path}", solutionPath);

        var solution = await _workspace!.OpenSolutionAsync(solutionPath, cancellationToken: ct);
        var csharpProjects = solution.Projects
            .Where(p => p.Language == LanguageNames.CSharp)
            .ToList();

        _logger.LogInformation("Solution contains {Count} C# projects", csharpProjects.Count);

        if (csharpProjects.Count == 0)
            throw new InvalidOperationException("Solution contains no C# projects");

        var compilationTasks = csharpProjects.Select(async proj =>
        {
            var comp = await proj.GetCompilationAsync(ct);
            return (proj, comp);
        }).ToList();

        var results = await Task.WhenAll(compilationTasks);

        foreach (var (proj, comp) in results)
        {
            if (comp != null)
            {
                _allCompilations[proj.Name] = comp;
            }
            else
            {
                _logger.LogWarning("Failed to compile project: {Name}", proj.Name);
            }
        }

        _project = csharpProjects.OrderByDescending(p => p.Documents.Count()).First();
        _compilation = _allCompilations.GetValueOrDefault(_project.Name)
            ?? throw new InvalidOperationException("Failed to get compilation for primary project");

        _logger.LogInformation("Primary project: {Name} ({DocCount} documents)",
            _project.Name, _project.Documents.Count());
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var path = _loadedPath;
        if (path == null) return;
        Dispose();
        await LoadProjectAsync(path, ct);
    }

    public IReadOnlyList<INamedTypeSymbol> FindTypesByName(string name)
    {
        var seen = new HashSet<string>();
        var results = new List<INamedTypeSymbol>();

        foreach (var compilation in _allCompilations.Values)
        {
            var byMetadata = compilation.GetTypeByMetadataName(name);
            if (byMetadata != null && byMetadata.Locations.Any(l => l.IsInSource))
            {
                var fqn = byMetadata.ToDisplayString();
                if (seen.Add(fqn))
                    results.Add(byMetadata);
                continue;
            }

            var symbols = compilation.GetSymbolsWithName(name, SymbolFilter.Type);
            foreach (var symbol in symbols.OfType<INamedTypeSymbol>())
            {
                if (!symbol.Locations.Any(l => l.IsInSource)) continue;
                var fqn = symbol.ToDisplayString();
                if (seen.Add(fqn))
                    results.Add(symbol);
            }
        }

        return results;
    }

    public IReadOnlyList<INamedTypeSymbol> GetAllSourceTypes()
    {
        var seen = new HashSet<string>();
        var results = new List<INamedTypeSymbol>();

        foreach (var compilation in _allCompilations.Values)
        {
            CollectTypes(compilation.GlobalNamespace, seen, results);
        }

        return results;
    }

    private static void CollectTypes(INamespaceSymbol ns, HashSet<string> seen, List<INamedTypeSymbol> results)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!type.Locations.Any(l => l.IsInSource)) continue;
            var fqn = type.ToDisplayString();
            if (seen.Add(fqn))
                results.Add(type);
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            CollectTypes(childNs, seen, results);
        }
    }

    public IReadOnlyList<INamedTypeSymbol> SearchTypes(string pattern, SymbolFilter filter = SymbolFilter.Type)
    {
        Func<string, bool> predicate;

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var re = new System.Text.RegularExpressions.Regex(regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            predicate = n => re.IsMatch(n);
        }
        else
        {
            predicate = n => n.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }

        var seen = new HashSet<string>();
        var results = new List<INamedTypeSymbol>();

        foreach (var compilation in _allCompilations.Values)
        {
            foreach (var symbol in compilation.GetSymbolsWithName(predicate, filter).OfType<INamedTypeSymbol>())
            {
                if (!symbol.Locations.Any(l => l.IsInSource)) continue;
                var fqn = symbol.ToDisplayString();
                if (seen.Add(fqn))
                    results.Add(symbol);
            }
        }

        return results;
    }

    public IReadOnlyList<ISymbol> FindSymbolsByName(string name, string? symbolKind = null, string? containingType = null)
    {
        if (symbolKind is "class" or "interface" or "enum" or "struct" or "delegate" or "type")
        {
            return FindTypesByName(name).Cast<ISymbol>().ToList();
        }

        if (!string.IsNullOrEmpty(containingType))
        {
            var containerTypes = FindTypesByName(containingType);
            if (containerTypes.Count == 0)
                return [];

            var results = new List<ISymbol>();
            foreach (var container in containerTypes)
            {
                results.AddRange(container.GetMembers(name)
                    .Where(m => !m.IsImplicitlyDeclared && MatchesSymbolKind(m, symbolKind)));
            }
            return results;
        }

        var allResults = new List<ISymbol>();

        if (symbolKind == null || symbolKind == "all")
        {
            var typeMatches = FindTypesByName(name);
            allResults.AddRange(typeMatches);
        }

        var seenMembers = new HashSet<string>();

        foreach (var compilation in _allCompilations.Values)
        {
            foreach (var member in compilation.GetSymbolsWithName(name, SymbolFilter.Member))
            {
                if (member.IsImplicitlyDeclared || !MatchesSymbolKind(member, symbolKind))
                    continue;
                if (!member.Locations.Any(l => l.IsInSource))
                    continue;

                var key = $"{member.ContainingType?.ToDisplayString()}.{member.Name}:{member.Kind}";
                if (seenMembers.Add(key))
                    allResults.Add(member);
            }
        }

        return allResults;
    }

    public Compilation GetCompilationForTree(SyntaxTree tree)
    {
        foreach (var comp in _allCompilations.Values)
        {
            if (comp.ContainsSyntaxTree(tree))
                return comp;
        }
        return Compilation;
    }

    public IEnumerable<(SyntaxTree Tree, Compilation Compilation)> GetAllSyntaxTrees()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var comp in _allCompilations.Values)
        {
            foreach (var tree in comp.SyntaxTrees)
            {
                if (seen.Add(tree.FilePath))
                    yield return (tree, comp);
            }
        }
    }

    public IReadOnlyList<Diagnostic> GetAllDiagnostics(CancellationToken ct = default)
    {
        var seen = new HashSet<string>();
        var results = new List<Diagnostic>();

        foreach (var comp in _allCompilations.Values)
        {
            foreach (var diag in comp.GetDiagnostics(ct))
            {
                var key = $"{diag.Location}|{diag.Id}|{diag.GetMessage()}";
                if (seen.Add(key))
                    results.Add(diag);
            }
        }

        return results;
    }

    private static bool MatchesSymbolKind(ISymbol symbol, string? kind)
    {
        if (string.IsNullOrEmpty(kind) || kind == "all") return true;
        return kind switch
        {
            "method" => symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary },
            "property" => symbol is IPropertySymbol,
            "field" => symbol is IFieldSymbol,
            "event" => symbol is IEventSymbol,
            _ => true
        };
    }

    public string? GetLineText(string filePath, int lineNumber)
    {
        try
        {
            foreach (var (tree, _) in GetAllSyntaxTrees())
            {
                if (tree.FilePath == filePath || Path.GetFullPath(tree.FilePath) == Path.GetFullPath(filePath))
                {
                    var text = tree.GetText();
                    if (lineNumber > 0 && lineNumber <= text.Lines.Count)
                    {
                        return text.Lines[lineNumber - 1].ToString().Trim();
                    }
                }
            }
        }
        catch
        {
        }
        return null;
    }

    public (string? FilePath, int Line) GetSymbolLocation(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (location == null) return (null, 0);

        var lineSpan = location.GetLineSpan();
        return (lineSpan.Path, lineSpan.StartLinePosition.Line + 1);
    }

    public void Dispose()
    {
        _workspace?.Dispose();
        _workspace = null;
        _project = null;
        _compilation = null;
        _allCompilations = new();
    }
}
