using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace ReservationSystem.Microservices.DatabaseMcp.Tools;

[McpServerToolType]
public sealed class DatabaseTools(IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _connectionString =
        config.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database is required");

    // -------------------------------------------------------------------------
    // run_select_query
    // -------------------------------------------------------------------------

    [McpServerTool]
    [Description("Execute a read-only SELECT query against the Apex Air database. Returns a JSON array of result rows. Only SELECT and WITH (CTE) statements are permitted. Semi-colons are blocked to prevent statement stacking.")]
    public async Task<string> RunSelectQuery(
        [Description("SQL SELECT query or CTE (WITH ... SELECT ...) to execute")] string sql,
        CancellationToken ct = default)
    {
        EnforceReadOnly(sql);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return JsonSerializer.Serialize(rows, JsonOpts);
    }

    // -------------------------------------------------------------------------
    // list_tables
    // -------------------------------------------------------------------------

    [McpServerTool]
    [Description("List all tables in the Apex Air database grouped by schema (offer, order, payment, delivery, customer, etc.). Use this to explore the data model before writing a query.")]
    public async Task<string> ListTables(CancellationToken ct = default)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var tables = new List<object>();
        while (await reader.ReadAsync(ct))
            tables.Add(new { schema = reader.GetString(0), table = reader.GetString(1) });

        return JsonSerializer.Serialize(tables, JsonOpts);
    }

    // -------------------------------------------------------------------------
    // describe_table
    // -------------------------------------------------------------------------

    [McpServerTool]
    [Description("Return the column definitions for a specific table — names, data types, nullability, max length, and default value. Use this before writing a query against an unfamiliar table.")]
    public async Task<string> DescribeTable(
        [Description("Schema name, e.g. 'offer', 'order', 'payment', 'delivery', 'customer', 'accounting', 'ancillary', 'schedule', 'identity', 'user'")] string schema,
        [Description("Table name exactly as it appears in the database, e.g. 'Offers', 'Orders', 'Tickets'")] string table,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COLUMN_NAME,
                DATA_TYPE,
                CHARACTER_MAXIMUM_LENGTH,
                IS_NULLABLE,
                COLUMN_DEFAULT
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME   = @table
            ORDER BY ORDINAL_POSITION
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);
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

        return JsonSerializer.Serialize(columns, JsonOpts);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void EnforceReadOnly(string sql)
    {
        var trimmed = sql.TrimStart();

        var isSelect = trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
        var isCte    = trimmed.StartsWith("WITH",   StringComparison.OrdinalIgnoreCase);

        if (!isSelect && !isCte)
            throw new InvalidOperationException("Only SELECT queries and CTEs are permitted. Mutation statements are blocked.");

        if (sql.Contains(';'))
            throw new InvalidOperationException("Semi-colons are not permitted. Only a single statement may be executed.");
    }
}
