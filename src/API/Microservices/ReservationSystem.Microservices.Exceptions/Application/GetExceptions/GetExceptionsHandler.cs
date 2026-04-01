using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Exceptions.Models.Responses;

namespace ReservationSystem.Microservices.Exceptions.Application.GetExceptions;

public sealed class AppInsightsOptions
{
    public const string SectionName = "AppInsights";
    public string WorkspaceId { get; set; } = string.Empty;
}

public sealed class GetExceptionsHandler
{
    private readonly LogsQueryClient _logsClient;
    private readonly AppInsightsOptions _options;
    private readonly ILogger<GetExceptionsHandler> _logger;

    public GetExceptionsHandler(
        LogsQueryClient logsClient,
        IOptions<AppInsightsOptions> options,
        ILogger<GetExceptionsHandler> logger)
    {
        _logsClient = logsClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GetExceptionsResponse> HandleAsync(CancellationToken ct)
    {
        var to = DateTimeOffset.UtcNow;
        var from = to.AddHours(-1);

        const string query = """
            exceptions
            | where timestamp >= ago(1h)
            | project
                timestamp,
                problemId,
                type,
                outerType,
                outerMessage,
                message = outerMessage,
                innermostType,
                innermostMessage,
                details,
                severityLevel,
                operation_Name,
                operation_Id,
                cloud_RoleName,
                assembly,
                method
            | order by timestamp desc
            """;

        _logger.LogInformation("Querying Application Insights exceptions from {From} to {To}", from, to);

        var response = await _logsClient.QueryWorkspaceAsync(
            _options.WorkspaceId,
            query,
            new QueryTimeRange(from, to),
            cancellationToken: ct);

        var entries = new List<ExceptionEntry>();

        foreach (var row in response.Value.Table.Rows)
        {
            var callStack = BuildCallStack(row);

            entries.Add(new ExceptionEntry
            {
                Timestamp = row.GetDateTimeOffset("timestamp") ?? from,
                ProblemId = row.GetString("problemId") ?? string.Empty,
                ExceptionType = row.GetString("innermostType") ?? row.GetString("type") ?? "Unknown",
                Message = row.GetString("innermostMessage") ?? row.GetString("outerMessage") ?? string.Empty,
                Method = row.GetString("method"),
                Assembly = row.GetString("assembly"),
                OuterType = row.GetString("outerType"),
                OuterMessage = row.GetString("outerMessage"),
                OperationName = row.GetString("operation_Name"),
                OperationId = row.GetString("operation_Id"),
                CloudRoleName = row.GetString("cloud_RoleName"),
                CallStack = callStack,
                SeverityLevel = row.GetInt32("severityLevel") ?? 3
            });
        }

        _logger.LogInformation("Found {Count} exceptions in the last hour", entries.Count);

        return new GetExceptionsResponse
        {
            Count = entries.Count,
            QueryFrom = from,
            QueryTo = to,
            Exceptions = entries.AsReadOnly()
        };
    }

    private static string BuildCallStack(LogsTableRow row)
    {
        // The 'details' column contains the parsed exception details with call stacks.
        // It is a dynamic (JSON) column — an array of objects each with
        // "parsedStack" (array of frame objects) and "message"/"type" fields.
        var details = row.GetString("details");
        if (string.IsNullOrWhiteSpace(details))
            return string.Empty;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(details);
            var root = doc.RootElement;

            if (root.ValueKind != System.Text.Json.JsonValueKind.Array)
                return details;

            var lines = new List<string>();

            foreach (var detail in root.EnumerateArray())
            {
                if (detail.TryGetProperty("type", out var typeProp))
                    lines.Add($"{typeProp.GetString()}: {GetOptionalString(detail, "message")}");

                if (detail.TryGetProperty("parsedStack", out var stack) &&
                    stack.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var frame in stack.EnumerateArray())
                    {
                        var method = GetOptionalString(frame, "method");
                        var assembly = GetOptionalString(frame, "assembly");
                        var fileName = GetOptionalString(frame, "fileName");
                        var line = GetOptionalInt(frame, "line");

                        var location = !string.IsNullOrEmpty(fileName) && line > 0
                            ? $" in {fileName}:line {line}"
                            : string.Empty;

                        lines.Add($"   at {method} [{assembly}]{location}");
                    }
                }

                lines.Add(string.Empty); // separator between chained exceptions
            }

            return string.Join('\n', lines).TrimEnd();
        }
        catch
        {
            // If parsing fails, return raw details
            return details;
        }
    }

    private static string GetOptionalString(System.Text.Json.JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) ? prop.GetString() ?? string.Empty : string.Empty;
    }

    private static int GetOptionalInt(System.Text.Json.JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) && prop.TryGetInt32(out var val) ? val : 0;
    }
}

internal static class LogsTableRowExtensions
{
    public static string? GetString(this LogsTableRow row, string column)
    {
        try { return row[column]?.ToString(); }
        catch { return null; }
    }

    public static DateTimeOffset? GetDateTimeOffset(this LogsTableRow row, string column)
    {
        try
        {
            var val = row[column];
            return val switch
            {
                DateTimeOffset dto => dto,
                DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),
                string s when DateTimeOffset.TryParse(s, out var parsed) => parsed,
                _ => null
            };
        }
        catch { return null; }
    }

    public static int? GetInt32(this LogsTableRow row, string column)
    {
        try
        {
            var val = row[column];
            return val switch
            {
                int i => i,
                long l => (int)l,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => null
            };
        }
        catch { return null; }
    }
}
