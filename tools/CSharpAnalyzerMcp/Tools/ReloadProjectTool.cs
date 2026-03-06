using System.ComponentModel;
using CSharpAnalyzerMcp.Services;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class ReloadProjectTool
{
    [McpServerTool(Name = "reload_project"),
     Description("Reload the project from disk. Call this after editing C# files to refresh the analysis cache. " +
                 "Takes ~5 seconds.")]
    public static async Task<string> Reload(WorkspaceService workspace)
    {
        await workspace.EnsureLoadedAsync();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await workspace.ReloadAsync();
        sw.Stop();

        return $"Project reloaded in {sw.ElapsedMilliseconds}ms. All tools now use fresh data.";
    }
}
