using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Microservices.Offer.Models.Database;
using ReservationSystem.Microservices.Offer.Models.Database.JsonFields;
using ReservationSystem.Microservices.Offer.Models.Mappers;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Microservices.Offer.Infrastructure.Persistence;

/// <summary>
/// SQL Server implementation of <see cref="IOfferRepository"/> using Dapper.
///
/// Data flow (read):
///   SQL row → <see cref="OfferRecord"/> (Dapper)
///   → deserialise <see cref="OfferRecord.Attributes"/> → <see cref="OfferAttributes"/>
///   → map to <see cref="Offer"/> domain entity via <see cref="OfferMapper"/>
///
/// Data flow (write):
///   <see cref="Offer"/> domain entity
///   → serialise metadata → <see cref="OfferAttributes"/> JSON string
///   → insert/update row via Dapper parameters
/// </summary>
public sealed class SqlOfferRepository : IOfferRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlOfferRepository> _logger;

    public SqlOfferRepository(
        SqlConnectionFactory connectionFactory,
        IOptions<DatabaseOptions> options,
        ILogger<SqlOfferRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Domain.Entities.Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, FlightNumber, Origin, Destination, DepartureAt,
                   FareClass, TotalPrice, Currency, Status, Attributes, CreatedAt, UpdatedAt
            FROM   [offer].[Offers]
            WHERE  Id = @Id;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var record = await connection.QuerySingleOrDefaultAsync<OfferRecord>(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: _options.CommandTimeoutSeconds));

        return record is null ? null : MapToDomain(record);
    }

    public async Task<IReadOnlyList<Domain.Entities.Offer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, FlightNumber, Origin, Destination, DepartureAt,
                   FareClass, TotalPrice, Currency, Status, Attributes, CreatedAt, UpdatedAt
            FROM   [offer].[Offers]
            ORDER  BY DepartureAt ASC;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var records = await connection.QueryAsync<OfferRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));

        return records.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task CreateAsync(Domain.Entities.Offer offer, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [offer].[Offers]
                (Id, FlightNumber, Origin, Destination, DepartureAt,
                 FareClass, TotalPrice, Currency, Status, Attributes, CreatedAt, UpdatedAt)
            VALUES
                (@Id, @FlightNumber, @Origin, @Destination, @DepartureAt,
                 @FareClass, @TotalPrice, @Currency, @Status, @Attributes, @CreatedAt, @UpdatedAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapToParameters(offer), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted Offer {Id} into [offer].[Offers]", offer.Id);
    }

    public async Task UpdateAsync(Domain.Entities.Offer offer, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [offer].[Offers]
            SET    FlightNumber = @FlightNumber,
                   Origin       = @Origin,
                   Destination  = @Destination,
                   DepartureAt  = @DepartureAt,
                   FareClass    = @FareClass,
                   TotalPrice   = @TotalPrice,
                   Currency     = @Currency,
                   Status       = @Status,
                   Attributes   = @Attributes,
                   UpdatedAt    = @UpdatedAt
            WHERE  Id = @Id;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, MapToParameters(offer), commandTimeout: _options.CommandTimeoutSeconds));

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Offer {Id}", offer.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [offer].[Offers] WHERE Id = @Id;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: _options.CommandTimeoutSeconds));
    }

    // -------------------------------------------------------------------------
    // Private mapping helpers
    // -------------------------------------------------------------------------

    private static Domain.Entities.Offer MapToDomain(OfferRecord record)
    {
        OfferAttributes? attributes = null;

        if (!string.IsNullOrWhiteSpace(record.Attributes))
        {
            attributes = JsonSerializer.Deserialize<OfferAttributes>(
                record.Attributes, SharedJsonOptions.CamelCase);
        }

        return OfferMapper.ToDomain(record, attributes);
    }

    private static object MapToParameters(Domain.Entities.Offer offer)
    {
        var attributes = OfferMapper.ToAttributes(offer);
        var attributesJson = JsonSerializer.Serialize(attributes, SharedJsonOptions.CamelCase);

        return new
        {
            offer.Id,
            offer.FlightNumber,
            offer.Origin,
            offer.Destination,
            offer.DepartureAt,
            offer.FareClass,
            offer.TotalPrice,
            offer.Currency,
            offer.Status,
            Attributes = attributesJson,
            offer.CreatedAt,
            offer.UpdatedAt
        };
    }
}
