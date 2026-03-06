using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharpAnalyzerMcp.Services;

MSBuildLocator.RegisterDefaults();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<WorkspaceService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

var workspace = app.Services.GetRequiredService<WorkspaceService>();
var projectPath = Environment.GetEnvironmentVariable("PROJECT_PATH") ?? "Hotel.csproj";
_ = Task.Run(() => workspace.InitializeAsync(projectPath));

await app.RunAsync();
