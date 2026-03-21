using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

namespace ReservationSystem.Microservices.Seat.Infrastructure.Persistence;

public sealed class SqlAircraftTypeRepository : IAircraftTypeRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlAircraftTypeRepository> _logger;

    public SqlAircraftTypeRepository(SqlConnectionFactory connectionFactory, IOptions<DatabaseOptions> options, ILogger<SqlAircraftTypeRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AircraftType?> GetByCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT AircraftTypeCode, Manufacturer, FriendlyName, TotalSeats, IsActive, CreatedAt, UpdatedAt
            FROM   [seat].[AircraftType]
            WHERE  AircraftTypeCode = @AircraftTypeCode;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleOrDefaultAsync<AircraftTypeRecord>(
            new CommandDefinition(sql, new { AircraftTypeCode = aircraftTypeCode }, commandTimeout: _options.CommandTimeoutSeconds));
        return r is null ? null : Map(r);
    }

    public async Task<IReadOnlyList<AircraftType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT AircraftTypeCode, Manufacturer, FriendlyName, TotalSeats, IsActive, CreatedAt, UpdatedAt
            FROM   [seat].[AircraftType]
            ORDER  BY AircraftTypeCode;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var records = await conn.QueryAsync<AircraftTypeRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));
        return records.Select(Map).ToList().AsReadOnly();
    }

    public async Task<AircraftType> CreateAsync(AircraftType at, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [seat].[AircraftType] (AircraftTypeCode, Manufacturer, FriendlyName, TotalSeats, IsActive)
            VALUES (@AircraftTypeCode, @Manufacturer, @FriendlyName, @TotalSeats, @IsActive);

            SELECT AircraftTypeCode, Manufacturer, FriendlyName, TotalSeats, IsActive, CreatedAt, UpdatedAt
            FROM   [seat].[AircraftType]
            WHERE  AircraftTypeCode = @AircraftTypeCode;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleAsync<AircraftTypeRecord>(
            new CommandDefinition(sql, new { at.AircraftTypeCode, at.Manufacturer, at.FriendlyName, at.TotalSeats, at.IsActive },
                commandTimeout: _options.CommandTimeoutSeconds));
        _logger.LogInformation("Created AircraftType {Code}", at.AircraftTypeCode);
        return Map(r);
    }

    public async Task<AircraftType?> UpdateAsync(AircraftType at, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [seat].[AircraftType]
            SET    Manufacturer = @Manufacturer, FriendlyName = @FriendlyName,
                   TotalSeats = @TotalSeats, IsActive = @IsActive
            WHERE  AircraftTypeCode = @AircraftTypeCode;

            SELECT AircraftTypeCode, Manufacturer, FriendlyName, TotalSeats, IsActive, CreatedAt, UpdatedAt
            FROM   [seat].[AircraftType]
            WHERE  AircraftTypeCode = @AircraftTypeCode;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleOrDefaultAsync<AircraftTypeRecord>(
            new CommandDefinition(sql, new { at.AircraftTypeCode, at.Manufacturer, at.FriendlyName, at.TotalSeats, at.IsActive },
                commandTimeout: _options.CommandTimeoutSeconds));
        return r is null ? null : Map(r);
    }

    public async Task<bool> DeleteAsync(string aircraftTypeCode, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [seat].[AircraftType] WHERE AircraftTypeCode = @AircraftTypeCode;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { AircraftTypeCode = aircraftTypeCode }, commandTimeout: _options.CommandTimeoutSeconds));
        return rows > 0;
    }

    private static AircraftType Map(AircraftTypeRecord r) =>
        AircraftType.Reconstitute(r.AircraftTypeCode, r.Manufacturer, r.FriendlyName, r.TotalSeats, r.IsActive, r.CreatedAt, r.UpdatedAt);

    private sealed record AircraftTypeRecord(
        string AircraftTypeCode, string Manufacturer, string? FriendlyName, int TotalSeats,
        bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
