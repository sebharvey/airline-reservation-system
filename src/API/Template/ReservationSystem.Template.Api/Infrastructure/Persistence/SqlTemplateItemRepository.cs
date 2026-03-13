using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Template.Api.Domain.Entities;
using ReservationSystem.Template.Api.Domain.Repositories;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Template.Api.Models.Database;
using ReservationSystem.Template.Api.Models.Database.JsonFields;
using ReservationSystem.Template.Api.Models.Mappers;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Template.Api.Infrastructure.Persistence;

/// <summary>
/// SQL Server implementation of <see cref="ITemplateItemRepository"/> using Dapper.
///
/// Data flow (read):
///   SQL row → <see cref="TemplateItemRecord"/> (Dapper)
///   → deserialise <see cref="TemplateItemRecord.AttributesJson"/> → <see cref="TemplateItemAttributes"/>
///   → map to <see cref="TemplateItem"/> domain entity via <see cref="TemplateItemMapper"/>
///
/// Data flow (write):
///   <see cref="TemplateItem"/> domain entity
///   → serialise metadata → <see cref="TemplateItemAttributes"/> JSON string
///   → insert/update row via Dapper parameters
/// </summary>
public sealed class SqlTemplateItemRepository : ITemplateItemRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlTemplateItemRepository> _logger;

    public SqlTemplateItemRepository(
        SqlConnectionFactory connectionFactory,
        IOptions<DatabaseOptions> options,
        ILogger<SqlTemplateItemRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TemplateItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Status, Attributes, CreatedAt, UpdatedAt
            FROM   [template].[Items]
            WHERE  Id = @Id;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var record = await connection.QuerySingleOrDefaultAsync<TemplateItemRecord>(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: _options.CommandTimeoutSeconds));

        return record is null ? null : MapToDomain(record);
    }

    public async Task<IReadOnlyList<TemplateItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Status, Attributes, CreatedAt, UpdatedAt
            FROM   [template].[Items]
            ORDER  BY CreatedAt DESC;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var records = await connection.QueryAsync<TemplateItemRecord>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));

        return records.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task CreateAsync(TemplateItem item, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO [template].[Items] (Id, Name, Status, Attributes, CreatedAt, UpdatedAt)
            VALUES (@Id, @Name, @Status, @Attributes, @CreatedAt, @UpdatedAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapToParameters(item), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted TemplateItem {Id} into [template].[Items]", item.Id);
    }

    public async Task UpdateAsync(TemplateItem item, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [template].[Items]
            SET    Name       = @Name,
                   Status     = @Status,
                   Attributes = @Attributes,
                   UpdatedAt  = @UpdatedAt
            WHERE  Id = @Id;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, MapToParameters(item), commandTimeout: _options.CommandTimeoutSeconds));

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for TemplateItem {Id}", item.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [template].[Items] WHERE Id = @Id;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: _options.CommandTimeoutSeconds));
    }

    // -------------------------------------------------------------------------
    // Private mapping helpers
    // -------------------------------------------------------------------------

    private static TemplateItem MapToDomain(TemplateItemRecord record)
    {
        TemplateItemAttributes? attributes = null;

        if (!string.IsNullOrWhiteSpace(record.Attributes))
        {
            attributes = JsonSerializer.Deserialize<TemplateItemAttributes>(
                record.Attributes, SharedJsonOptions.CamelCase);
        }

        return TemplateItemMapper.ToDomain(record, attributes);
    }

    private static object MapToParameters(TemplateItem item)
    {
        var attributes = TemplateItemMapper.ToAttributes(item);
        var attributesJson = JsonSerializer.Serialize(attributes, SharedJsonOptions.CamelCase);

        return new
        {
            item.Id,
            item.Name,
            item.Status,
            Attributes = attributesJson,
            item.CreatedAt,
            item.UpdatedAt
        };
    }
}
