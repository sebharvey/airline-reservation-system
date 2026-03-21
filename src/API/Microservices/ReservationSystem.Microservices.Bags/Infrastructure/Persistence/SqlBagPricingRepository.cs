using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Bags.Domain.Entities;
using ReservationSystem.Microservices.Bags.Domain.Repositories;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

namespace ReservationSystem.Microservices.Bags.Infrastructure.Persistence;

public sealed class SqlBagPricingRepository : IBagPricingRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlBagPricingRepository> _logger;

    public SqlBagPricingRepository(
        SqlConnectionFactory connectionFactory,
        IOptions<DatabaseOptions> options,
        ILogger<SqlBagPricingRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BagPricing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT PricingId, BagSequence, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [bag].[BagPricing]
            WHERE  PricingId = @PricingId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<BagPricingRecord>(
            new CommandDefinition(sql, new { PricingId = id }, commandTimeout: _options.CommandTimeoutSeconds));

        return record is null ? null : MapToDomain(record);
    }

    public async Task<BagPricing?> GetBySequenceAsync(int bagSequence, string currencyCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT PricingId, BagSequence, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [bag].[BagPricing]
            WHERE  BagSequence = @BagSequence AND CurrencyCode = @CurrencyCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<BagPricingRecord>(
            new CommandDefinition(sql, new { BagSequence = bagSequence, CurrencyCode = currencyCode },
                commandTimeout: _options.CommandTimeoutSeconds));

        return record is null ? null : MapToDomain(record);
    }

    public async Task<IReadOnlyList<BagPricing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT PricingId, BagSequence, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [bag].[BagPricing]
            ORDER  BY BagSequence;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var records = await connection.QueryAsync<BagPricingRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));

        return records.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<BagPricing>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT PricingId, BagSequence, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [bag].[BagPricing]
            WHERE  IsActive = 1
            ORDER  BY BagSequence;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var records = await connection.QueryAsync<BagPricingRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));

        return records.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task<BagPricing> CreateAsync(BagPricing pricing, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [bag].[BagPricing] (PricingId, BagSequence, CurrencyCode, Price, IsActive, ValidFrom, ValidTo)
            VALUES (@PricingId, @BagSequence, @CurrencyCode, @Price, @IsActive, @ValidFrom, @ValidTo);

            SELECT PricingId, BagSequence, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [bag].[BagPricing]
            WHERE  PricingId = @PricingId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleAsync<BagPricingRecord>(
            new CommandDefinition(sql, new
            {
                pricing.PricingId,
                pricing.BagSequence,
                pricing.CurrencyCode,
                pricing.Price,
                pricing.IsActive,
                pricing.ValidFrom,
                pricing.ValidTo
            }, commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogInformation("Created BagPricing {PricingId} for sequence {BagSequence}", pricing.PricingId, pricing.BagSequence);
        return MapToDomain(record);
    }

    public async Task<BagPricing?> UpdateAsync(BagPricing pricing, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [bag].[BagPricing]
            SET    Price     = @Price,
                   IsActive  = @IsActive,
                   ValidFrom = @ValidFrom,
                   ValidTo   = @ValidTo
            WHERE  PricingId = @PricingId;

            SELECT PricingId, BagSequence, CurrencyCode, Price, IsActive, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [bag].[BagPricing]
            WHERE  PricingId = @PricingId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<BagPricingRecord>(
            new CommandDefinition(sql, new
            {
                pricing.PricingId,
                pricing.Price,
                pricing.IsActive,
                pricing.ValidFrom,
                pricing.ValidTo
            }, commandTimeout: _options.CommandTimeoutSeconds));

        if (record is null)
        {
            _logger.LogWarning("UpdateAsync found no row for BagPricing {PricingId}", pricing.PricingId);
            return null;
        }

        return MapToDomain(record);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [bag].[BagPricing] WHERE PricingId = @PricingId;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { PricingId = id }, commandTimeout: _options.CommandTimeoutSeconds));

        return rowsAffected > 0;
    }

    private static BagPricing MapToDomain(BagPricingRecord r) =>
        BagPricing.Reconstitute(r.PricingId, r.BagSequence, r.CurrencyCode, r.Price,
            r.IsActive, r.ValidFrom, r.ValidTo, r.CreatedAt, r.UpdatedAt);

    private sealed record BagPricingRecord(
        Guid PricingId, int BagSequence, string CurrencyCode, decimal Price,
        bool IsActive, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
