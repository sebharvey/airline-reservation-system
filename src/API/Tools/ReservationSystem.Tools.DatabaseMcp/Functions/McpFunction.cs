using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReservationSystem.Tools.DatabaseMcp.Functions;

/// <summary>
/// Remote MCP server over HTTP Streamable transport (JSON-RPC 2.0 over POST).
/// Implements: initialize, tools/list, tools/call.
/// Secured by the Azure Function host key (x-functions-key header).
/// </summary>
public sealed class McpFunction(IConfiguration config)
{
    private static readonly JsonSerializerOptions ResultJson = new() { WriteIndented = true };

    private string ConnectionString => config.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured");

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/mcp  —  MCP HTTP Streamable endpoint
    // ─────────────────────────────────────────────────────────────────────────

    [Function("Mcp")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mcp")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadAsStringAsync() ?? "";

        JsonNode? msg;
        try { msg = JsonNode.Parse(body); }
        catch { return await ErrorResponse(req, null, -32700, "Parse error"); }

        // Notifications have no id — acknowledge without a body
        if (msg?["id"] is null)
            return req.CreateResponse(HttpStatusCode.Accepted);

        var id     = msg["id"];
        var method = msg["method"]?.GetValue<string>();

        try
        {
            var result = method switch
            {
                "initialize"  => HandleInitialize(),
                "ping"        => new JsonObject(),
                "tools/list"  => HandleToolsList(),
                "tools/call"  => await HandleToolsCall(msg, ct),
                _             => throw new McpMethodException(-32601, $"Method not found: {method}")
            };

            return await OkJson(req, RpcResult(id, result));
        }
        catch (McpMethodException ex)
        {
            return await OkJson(req, RpcError(id, ex.Code, ex.Message));
        }
        catch (Exception ex)
        {
            return await OkJson(req, RpcError(id, -32603, ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MCP protocol handlers
    // ─────────────────────────────────────────────────────────────────────────

    private static JsonNode HandleInitialize() =>
        JsonSerializer.SerializeToNode(new
        {
            protocolVersion = "2024-11-05",
            capabilities    = new { tools = new { } },
            serverInfo      = new { name = "apex-air-db", version = "1.0.0" }
        })!;

    private static JsonNode HandleToolsList() =>
        JsonSerializer.SerializeToNode(new
        {
            tools = new object[]
            {
                Tool("run_select_query",
                    "Execute a read-only SELECT query or CTE against the Apex Air database. Returns a JSON array of result rows. Only SELECT and WITH statements are permitted; semi-colons are blocked.",
                    new { sql = Param("string", "SQL SELECT or WITH ... SELECT ... query to execute") },
                    required: ["sql"]),

                Tool("list_tables",
                    "List all tables in the Apex Air database grouped by schema (offer, order, payment, delivery, customer, etc.). Use this first to discover what data exists.",
                    new { }),

                Tool("describe_table",
                    "Return column definitions for a specific table — names, data types, nullability, and defaults. Use before writing a query against an unfamiliar table.",
                    new
                    {
                        schema = Param("string", "Schema name, e.g. 'offer', 'order', 'payment', 'delivery', 'customer', 'accounting'"),
                        table  = Param("string", "Table name exactly as it appears in the database, e.g. 'Offers', 'Orders', 'Tickets'")
                    },
                    required: ["schema", "table"])
            }
        })!;

    private async Task<JsonNode> HandleToolsCall(JsonNode msg, CancellationToken ct)
    {
        var toolName = msg["params"]?["name"]?.GetValue<string>()
            ?? throw new McpMethodException(-32602, "params.name is required");

        var args = msg["params"]?["arguments"] as JsonObject ?? [];

        string text;
        bool   isError = false;

        try
        {
            text = toolName switch
            {
                "run_select_query" => await RunSelectQuery(args["sql"]?.GetValue<string>() ?? "", ct),
                "list_tables"      => await ListTables(ct),
                "describe_table"   => await DescribeTable(
                                          args["schema"]?.GetValue<string>() ?? "",
                                          args["table"]?.GetValue<string>()  ?? "",
                                          ct),
                _ => throw new McpMethodException(-32602, $"Unknown tool: {toolName}")
            };
        }
        catch (McpMethodException) { throw; }
        catch (Exception ex) { text = ex.Message; isError = true; }

        var result = new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text })
        };
        if (isError) result["isError"] = true;

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SQL tools
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<string> RunSelectQuery(string sql, CancellationToken ct)
    {
        EnforceReadOnly(sql);

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return JsonSerializer.Serialize(rows, ResultJson);
    }

    private async Task<string> ListTables(CancellationToken ct)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM   INFORMATION_SCHEMA.TABLES
            WHERE  TABLE_TYPE = 'BASE TABLE'
            ORDER  BY TABLE_SCHEMA, TABLE_NAME
            """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var tables = new List<object>();
        while (await reader.ReadAsync(ct))
            tables.Add(new { schema = reader.GetString(0), table = reader.GetString(1) });

        return JsonSerializer.Serialize(tables, ResultJson);
    }

    private async Task<string> DescribeTable(string schema, string table, CancellationToken ct)
    {
        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, COLUMN_DEFAULT
            FROM   INFORMATION_SCHEMA.COLUMNS
            WHERE  TABLE_SCHEMA = @schema
              AND  TABLE_NAME   = @table
            ORDER  BY ORDINAL_POSITION
            """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table",  table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var columns = new List<object>();
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new
            {
                columnName   = reader.GetString(0),
                dataType     = reader.GetString(1),
                maxLength    = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                isNullable   = reader.GetString(3) == "YES",
                defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return JsonSerializer.Serialize(columns, ResultJson);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void EnforceReadOnly(string sql)
    {
        var t = sql.TrimStart();
        if (!t.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !t.StartsWith("WITH",   StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only SELECT queries and CTEs are permitted");

        if (sql.Contains(';'))
            throw new InvalidOperationException("Semi-colons are not permitted — only one statement may be executed");
    }

    private static object Param(string type, string description) => new { type, description };

    private static object Tool(string name, string description, object properties, string[]? required = null) =>
        new
        {
            name,
            description,
            inputSchema = new
            {
                type = "object",
                properties,
                required = required ?? Array.Empty<string>()
            }
        };

    private static JsonObject RpcResult(JsonNode? id, JsonNode result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = result };

    private static JsonObject RpcError(JsonNode? id, int code, string message) =>
        new()
        {
            ["jsonrpc"] = "2.0",
            ["id"]      = id?.DeepClone(),
            ["error"]   = new JsonObject { ["code"] = code, ["message"] = message }
        };

    private static async Task<HttpResponseData> OkJson(HttpRequestData req, JsonNode node)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json");
        await res.WriteStringAsync(node.ToJsonString());
        return res;
    }

    private static Task<HttpResponseData> ErrorResponse(HttpRequestData req, JsonNode? id, int code, string message) =>
        OkJson(req, RpcError(id, code, message));
}

file sealed class McpMethodException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
