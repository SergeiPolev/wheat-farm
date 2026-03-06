using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using CSharpAnalyzerMcp.Services;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools;

[McpServerToolType]
public static class BatchExecuteTool
{
    private static readonly Dictionary<string, ToolEntry> ToolMap = BuildToolMap();

    [McpServerTool(Name = "batch_execute"),
     Description("Execute multiple analyzer tools in a single call to reduce round-trips. " +
                  "Max 10 operations per batch. Each operation runs independently — " +
                  "one failure doesn't affect others. " +
                  "Example: [{\"tool\": \"get_class_info\", \"params\": {\"className\": \"WalletService\"}}, " +
                  "{\"tool\": \"get_class_info\", \"params\": {\"className\": \"HotelService\"}}]")]
    public static async Task<string> Execute(
        WorkspaceService workspace,
        [Description("JSON array of operations. Each object: {\"tool\": \"<tool_name>\", \"params\": {<tool_params>}}. " +
                      "Max 10 operations. Cannot include 'reload_project' or 'batch_execute'.")]
        string operations)
    {
        await workspace.EnsureLoadedAsync();
        List<BatchOperation> ops;
        try
        {
            ops = JsonSerializer.Deserialize<List<BatchOperation>>(operations, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch (JsonException ex)
        {
            return ToonFormat.Toon.Encode(new { error = $"Invalid JSON: {ex.Message}" });
        }

        if (ops.Count == 0)
            return ToonFormat.Toon.Encode(new { error = "No operations provided." });

        if (ops.Count > 10)
            return ToonFormat.Toon.Encode(new { error = $"Maximum 10 operations per batch. Got: {ops.Count}" });

        var results = new List<object>();
        var succeeded = 0;
        var failed = 0;

        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            try
            {
                if (string.IsNullOrEmpty(op.Tool))
                    throw new ArgumentException("Missing 'tool' field.");

                if (op.Tool is "batch_execute" or "reload_project")
                    throw new ArgumentException($"Tool '{op.Tool}' cannot be used inside batch_execute.");

                if (!ToolMap.TryGetValue(op.Tool, out var entry))
                    throw new ArgumentException($"Unknown tool: '{op.Tool}'. Available: {string.Join(", ", ToolMap.Keys.Order())}");

                var data = await entry.InvokeAsync(workspace, op.Params);

                results.Add(new { index = i, tool = op.Tool, status = "success", data });
                succeeded++;
            }
            catch (Exception ex)
            {
                var message = ex is TargetInvocationException tie ? tie.InnerException?.Message ?? ex.Message : ex.Message;
                results.Add(new { index = i, tool = op.Tool ?? "<missing>", status = "error", error = message });
                failed++;
            }
        }

        return ToonFormat.Toon.Encode(new
        {
            results,
            summary = new { total = ops.Count, succeeded, failed }
        });
    }

    private static Dictionary<string, ToolEntry> BuildToolMap()
    {
        var map = new Dictionary<string, ToolEntry>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(BatchExecuteTool).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() == null) continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr == null) continue;

                var toolName = toolAttr.Name ?? method.Name;
                map[toolName] = new ToolEntry(method);
            }
        }

        return map;
    }

    private sealed class ToolEntry
    {
        private readonly MethodInfo _method;
        private readonly ParameterInfo[] _parameters;

        public ToolEntry(MethodInfo method)
        {
            _method = method;
            _parameters = method.GetParameters();
        }

        public async Task<string> InvokeAsync(WorkspaceService workspace, JsonElement parameters)
        {
            var args = new object?[_parameters.Length];

            for (var i = 0; i < _parameters.Length; i++)
            {
                var param = _parameters[i];

                if (param.ParameterType == typeof(WorkspaceService))
                {
                    args[i] = workspace;
                    continue;
                }

                if (parameters.TryGetProperty(param.Name!, out var jsonValue))
                {
                    args[i] = DeserializeParam(jsonValue, param.ParameterType);
                }
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                }
                else
                {
                    throw new ArgumentException($"Required parameter '{param.Name}' not provided.");
                }
            }

            var result = _method.Invoke(null, args);

            if (result is Task<string> taskString)
                return await taskString;

            if (result is string str)
                return str;

            return result?.ToString() ?? "";
        }

        private static object? DeserializeParam(JsonElement element, Type targetType)
        {
            if (targetType == typeof(string))
                return element.GetString();

            if (targetType == typeof(int))
                return element.GetInt32();

            if (targetType == typeof(int?))
                return element.ValueKind == JsonValueKind.Null ? null : element.GetInt32();

            if (targetType == typeof(bool))
                return element.GetBoolean();

            if (targetType == typeof(bool?))
                return element.ValueKind == JsonValueKind.Null ? null : element.GetBoolean();

            return element.Deserialize(targetType);
        }
    }

    private class BatchOperation
    {
        public string? Tool { get; set; }
        public JsonElement Params { get; set; }
    }
}
