using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

namespace ReservationSystem.Microservices.Ancillary.Infrastructure.Persistence.Bag;

public sealed class SqlBagPolicyRepository : IBagPolicyRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlBagPolicyRepository> _logger;

    public SqlBagPolicyRepository(
        SqlConnectionFactory connectionFactory,
        IOptions<DatabaseOptions> options,
        ILogger<SqlBagPolicyRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BagPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT PolicyId, CabinCode, FreeBagsIncluded, MaxWeightKgPerBag, IsActive, CreatedAt, UpdatedAt
            FROM   [bag].[BagPolicy]
            WHERE  PolicyId = @PolicyId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<BagPolicyRecord>(
            new CommandDefinition(sql, new { PolicyId = id }, commandTimeout: _options.CommandTimeoutSeconds));

        return record is null ? null : MapToDomain(record);
    }

    public async Task<BagPolicy?> GetByCabinCodeAsync(string cabinCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT PolicyId, CabinCode, FreeBagsIncluded, MaxWeightKgPerBag, IsActive, CreatedAt, UpdatedAt
            FROM   [bag].[BagPolicy]
            WHERE  CabinCode = @CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<BagPolicyRecord>(
            new CommandDefinition(sql, new { CabinCode = cabinCode }, commandTimeout: _options.CommandTimeoutSeconds));

        return record is null ? null : MapToDomain(record);
    }

    public async Task<IReadOnlyList<BagPolicy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT PolicyId, CabinCode, FreeBagsIncluded, MaxWeightKgPerBag, IsActive, CreatedAt, UpdatedAt
            FROM   [bag].[BagPolicy]
            ORDER  BY CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var records = await connection.QueryAsync<BagPolicyRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));

        return records.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task<BagPolicy> CreateAsync(BagPolicy policy, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [bag].[BagPolicy] (PolicyId, CabinCode, FreeBagsIncluded, MaxWeightKgPerBag, IsActive)
            VALUES (@PolicyId, @CabinCode, @FreeBagsIncluded, @MaxWeightKgPerBag, @IsActive);

            SELECT PolicyId, CabinCode, FreeBagsIncluded, MaxWeightKgPerBag, IsActive, CreatedAt, UpdatedAt
            FROM   [bag].[BagPolicy]
            WHERE  PolicyId = @PolicyId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleAsync<BagPolicyRecord>(
            new CommandDefinition(sql, new
            {
                policy.PolicyId,
                policy.CabinCode,
                policy.FreeBagsIncluded,
                policy.MaxWeightKgPerBag,
                policy.IsActive
            }, commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogInformation("Created BagPolicy {PolicyId} for cabin {CabinCode}", policy.PolicyId, policy.CabinCode);
        return MapToDomain(record);
    }

    public async Task<BagPolicy?> UpdateAsync(BagPolicy policy, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [bag].[BagPolicy]
            SET    FreeBagsIncluded  = @FreeBagsIncluded,
                   MaxWeightKgPerBag = @MaxWeightKgPerBag,
                   IsActive          = @IsActive
            WHERE  PolicyId = @PolicyId;

            SELECT PolicyId, CabinCode, FreeBagsIncluded, MaxWeightKgPerBag, IsActive, CreatedAt, UpdatedAt
            FROM   [bag].[BagPolicy]
            WHERE  PolicyId = @PolicyId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<BagPolicyRecord>(
            new CommandDefinition(sql, new
            {
                policy.PolicyId,
                policy.FreeBagsIncluded,
                policy.MaxWeightKgPerBag,
                policy.IsActive
            }, commandTimeout: _options.CommandTimeoutSeconds));

        if (record is null)
        {
            _logger.LogWarning("UpdateAsync found no row for BagPolicy {PolicyId}", policy.PolicyId);
            return null;
        }

        return MapToDomain(record);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [bag].[BagPolicy] WHERE PolicyId = @PolicyId;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { PolicyId = id }, commandTimeout: _options.CommandTimeoutSeconds));

        return rowsAffected > 0;
    }

    private static BagPolicy MapToDomain(BagPolicyRecord r) =>
        BagPolicy.Reconstitute(r.PolicyId, r.CabinCode, r.FreeBagsIncluded, r.MaxWeightKgPerBag,
            r.IsActive, r.CreatedAt, r.UpdatedAt);

    private sealed record BagPolicyRecord(
        Guid PolicyId, string CabinCode, int FreeBagsIncluded, int MaxWeightKgPerBag,
        bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
}
