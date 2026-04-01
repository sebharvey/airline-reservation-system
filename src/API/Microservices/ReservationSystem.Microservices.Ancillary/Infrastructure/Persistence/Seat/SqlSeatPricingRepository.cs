using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Seat;

public sealed class SqlSeatPricingRepository : ISeatPricingRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlSeatPricingRepository> _logger;

    public SqlSeatPricingRepository(SqlConnectionFactory connectionFactory, IOptions<DatabaseOptions> options, ILogger<SqlSeatPricingRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SeatPricing?> GetByIdAsync(Guid seatPricingId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SeatPricingId, CabinCode, SeatPosition, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [seat].[SeatPricing]
            WHERE  SeatPricingId = @SeatPricingId;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleOrDefaultAsync<SeatPricingRecord>(
            new CommandDefinition(sql, new { SeatPricingId = seatPricingId }, commandTimeout: _options.CommandTimeoutSeconds));
        return r is null ? null : Map(r);
    }

    public async Task<IReadOnlyList<SeatPricing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SeatPricingId, CabinCode, SeatPosition, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [seat].[SeatPricing]
            ORDER  BY CabinCode, SeatPosition;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var records = await conn.QueryAsync<SeatPricingRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));
        return records.Select(Map).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<SeatPricing>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SeatPricingId, CabinCode, SeatPosition, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [seat].[SeatPricing]
            WHERE  IsActive = 1
            ORDER  BY CabinCode, SeatPosition;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var records = await conn.QueryAsync<SeatPricingRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));
        return records.Select(Map).ToList().AsReadOnly();
    }

    public async Task<SeatPricing> CreateAsync(SeatPricing sp, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [seat].[SeatPricing] (SeatPricingId, CabinCode, SeatPosition, CurrencyCode, Price, IsActive, ValidFrom, ValidTo)
            VALUES (@SeatPricingId, @CabinCode, @SeatPosition, @CurrencyCode, @Price, @IsActive, @ValidFrom, @ValidTo);

            SELECT SeatPricingId, CabinCode, SeatPosition, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [seat].[SeatPricing]
            WHERE  SeatPricingId = @SeatPricingId;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleAsync<SeatPricingRecord>(
            new CommandDefinition(sql, new
            {
                sp.SeatPricingId, sp.CabinCode, sp.SeatPosition, sp.CurrencyCode, sp.Price, sp.IsActive, sp.ValidFrom, sp.ValidTo
            }, commandTimeout: _options.CommandTimeoutSeconds));
        _logger.LogInformation("Created SeatPricing {Id} for {Cabin}/{Position}", sp.SeatPricingId, sp.CabinCode, sp.SeatPosition);
        return Map(r);
    }

    public async Task<SeatPricing?> UpdateAsync(SeatPricing sp, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [seat].[SeatPricing]
            SET    Price = @Price, IsActive = @IsActive, ValidFrom = @ValidFrom, ValidTo = @ValidTo
            WHERE  SeatPricingId = @SeatPricingId;

            SELECT SeatPricingId, CabinCode, SeatPosition, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [seat].[SeatPricing]
            WHERE  SeatPricingId = @SeatPricingId;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleOrDefaultAsync<SeatPricingRecord>(
            new CommandDefinition(sql, new { sp.SeatPricingId, sp.Price, sp.IsActive, sp.ValidFrom, sp.ValidTo },
                commandTimeout: _options.CommandTimeoutSeconds));
        return r is null ? null : Map(r);
    }

    public async Task<bool> DeleteAsync(Guid seatPricingId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [seat].[SeatPricing] WHERE SeatPricingId = @SeatPricingId;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { SeatPricingId = seatPricingId }, commandTimeout: _options.CommandTimeoutSeconds));
        return rows > 0;
    }

    private static SeatPricing Map(SeatPricingRecord r) =>
        SeatPricing.Reconstitute(r.SeatPricingId, r.CabinCode, r.SeatPosition, r.CurrencyCode, r.Price,
            r.IsActive, r.ValidFrom, r.ValidTo, r.CreatedAt, r.UpdatedAt);

    private sealed record SeatPricingRecord(
        Guid SeatPricingId, string CabinCode, string SeatPosition, string CurrencyCode, decimal Price,
        bool IsActive, DateTime ValidFrom, DateTime? ValidTo,
        DateTime CreatedAt, DateTime UpdatedAt);
}
