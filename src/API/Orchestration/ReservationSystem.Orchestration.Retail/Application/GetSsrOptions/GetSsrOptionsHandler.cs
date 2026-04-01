using Dapper;
using Microsoft.Extensions.Options;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

namespace ReservationSystem.Orchestration.Retail.Application.GetSsrOptions;

public sealed class GetSsrOptionsHandler
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;

    public GetSsrOptionsHandler(SqlConnectionFactory connectionFactory, IOptions<DatabaseOptions> options)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
    }

    public async Task<GetSsrOptionsResponse> HandleAsync(GetSsrOptionsQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SsrCode, Label, Category
            FROM   [retail].[SsrCatalogue]
            WHERE  IsActive = 1
            ORDER  BY Category, SsrCode;
            """;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SsrOptionDto>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken));

        return new GetSsrOptionsResponse(rows.ToList().AsReadOnly());
    }
}
