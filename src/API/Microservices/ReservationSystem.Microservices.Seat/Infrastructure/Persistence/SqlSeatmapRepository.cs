using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

namespace ReservationSystem.Microservices.Seat.Infrastructure.Persistence;

public sealed class SqlSeatmapRepository : ISeatmapRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlSeatmapRepository> _logger;

    public SqlSeatmapRepository(SqlConnectionFactory connectionFactory, IOptions<DatabaseOptions> options, ILogger<SqlSeatmapRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Seatmap?> GetByIdAsync(Guid seatmapId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SeatmapId, AircraftTypeCode, Version, IsActive, CabinLayout, CreatedAt, UpdatedAt
            FROM   [seat].[Seatmap]
            WHERE  SeatmapId = @SeatmapId;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleOrDefaultAsync<SeatmapRecord>(
            new CommandDefinition(sql, new { SeatmapId = seatmapId }, commandTimeout: _options.CommandTimeoutSeconds));
        return r is null ? null : Map(r);
    }

    public async Task<Seatmap?> GetActiveByAircraftTypeCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SeatmapId, AircraftTypeCode, Version, IsActive, CabinLayout, CreatedAt, UpdatedAt
            FROM   [seat].[Seatmap]
            WHERE  AircraftTypeCode = @AircraftTypeCode AND IsActive = 1;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleOrDefaultAsync<SeatmapRecord>(
            new CommandDefinition(sql, new { AircraftTypeCode = aircraftTypeCode }, commandTimeout: _options.CommandTimeoutSeconds));
        return r is null ? null : Map(r);
    }

    public async Task<IReadOnlyList<Seatmap>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // List view excludes CabinLayout for performance
        const string sql = """
            SELECT SeatmapId, AircraftTypeCode, Version, IsActive, '' as CabinLayout, CreatedAt, UpdatedAt
            FROM   [seat].[Seatmap]
            ORDER  BY AircraftTypeCode, Version DESC;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var records = await conn.QueryAsync<SeatmapRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));
        return records.Select(Map).ToList().AsReadOnly();
    }

    public async Task<Seatmap> CreateAsync(Seatmap seatmap, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [seat].[Seatmap] (SeatmapId, AircraftTypeCode, Version, IsActive, CabinLayout)
            VALUES (@SeatmapId, @AircraftTypeCode, @Version, @IsActive, @CabinLayout);

            SELECT SeatmapId, AircraftTypeCode, Version, IsActive, CabinLayout, CreatedAt, UpdatedAt
            FROM   [seat].[Seatmap]
            WHERE  SeatmapId = @SeatmapId;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleAsync<SeatmapRecord>(
            new CommandDefinition(sql, new
            {
                seatmap.SeatmapId, seatmap.AircraftTypeCode, seatmap.Version, seatmap.IsActive, seatmap.CabinLayout
            }, commandTimeout: _options.CommandTimeoutSeconds));
        _logger.LogInformation("Created Seatmap {SeatmapId} for {AircraftType}", seatmap.SeatmapId, seatmap.AircraftTypeCode);
        return Map(r);
    }

    public async Task<Seatmap?> UpdateAsync(Seatmap seatmap, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [seat].[Seatmap]
            SET    CabinLayout = @CabinLayout, IsActive = @IsActive
            WHERE  SeatmapId = @SeatmapId;

            SELECT SeatmapId, AircraftTypeCode, Version, IsActive, CabinLayout, CreatedAt, UpdatedAt
            FROM   [seat].[Seatmap]
            WHERE  SeatmapId = @SeatmapId;
            """;
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var r = await conn.QuerySingleOrDefaultAsync<SeatmapRecord>(
            new CommandDefinition(sql, new { seatmap.SeatmapId, seatmap.CabinLayout, seatmap.IsActive },
                commandTimeout: _options.CommandTimeoutSeconds));
        return r is null ? null : Map(r);
    }

    public async Task<bool> DeleteAsync(Guid seatmapId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [seat].[Seatmap] WHERE SeatmapId = @SeatmapId;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { SeatmapId = seatmapId }, commandTimeout: _options.CommandTimeoutSeconds));
        return rows > 0;
    }

    public async Task DeactivateByAircraftTypeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE [seat].[Seatmap] SET IsActive = 0 WHERE AircraftTypeCode = @AircraftTypeCode AND IsActive = 1;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { AircraftTypeCode = aircraftTypeCode }, commandTimeout: _options.CommandTimeoutSeconds));
    }

    public async Task<bool> HasActiveSeatmapsAsync(string aircraftTypeCode, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(1) FROM [seat].[Seatmap] WHERE AircraftTypeCode = @AircraftTypeCode AND IsActive = 1;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { AircraftTypeCode = aircraftTypeCode }, commandTimeout: _options.CommandTimeoutSeconds));
        return count > 0;
    }

    private static Seatmap Map(SeatmapRecord r) =>
        Seatmap.Reconstitute(r.SeatmapId, r.AircraftTypeCode, r.Version, r.IsActive, r.CabinLayout, r.CreatedAt, r.UpdatedAt);

    private sealed record SeatmapRecord(
        Guid SeatmapId, string AircraftTypeCode, int Version, bool IsActive,
        string CabinLayout, DateTime CreatedAt, DateTime UpdatedAt);
}
