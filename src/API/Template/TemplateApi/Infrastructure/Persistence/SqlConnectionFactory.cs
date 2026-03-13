using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ReservationSystem.Template.TemplateApi.Infrastructure.Configuration;
using System.Data;

namespace ReservationSystem.Template.TemplateApi.Infrastructure.Persistence;

/// <summary>
/// Creates and opens SQL connections using the configured connection string.
/// Callers are responsible for disposing the returned connection.
/// </summary>
public sealed class SqlConnectionFactory
{
    private readonly DatabaseOptions _options;

    public SqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Returns a new, open <see cref="IDbConnection"/> to the configured Azure SQL database.
    /// </summary>
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
