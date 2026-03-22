using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Template.TemplateApi.Domain.Entities;
using ReservationSystem.Template.TemplateApi.Domain.Repositories;
using ReservationSystem.Template.TemplateApi.Models.Database;
using ReservationSystem.Template.TemplateApi.Models.Database.JsonFields;
using ReservationSystem.Template.TemplateApi.Models.Mappers;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Template.TemplateApi.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="ITemplateItemRepository"/>.
///
/// Uses <see cref="TemplateItemsDbContext"/> to interact with the [template].[Items] table.
///
/// Data flow (read):
///   EF query → <see cref="TemplateItemRecord"/>
///   → deserialise <see cref="TemplateItemRecord.Attributes"/> → <see cref="TemplateItemAttributes"/>
///   → map to <see cref="TemplateItem"/> domain entity via <see cref="TemplateItemMapper"/>
///
/// Data flow (write):
///   <see cref="TemplateItem"/> domain entity
///   → serialise metadata → <see cref="TemplateItemAttributes"/> JSON string
///   → insert/update row via EF Core
/// </summary>
public sealed class EfTemplateItemRepository : ITemplateItemRepository
{
    private readonly TemplateItemsDbContext _dbContext;
    private readonly ILogger<EfTemplateItemRepository> _logger;

    public EfTemplateItemRepository(
        TemplateItemsDbContext dbContext,
        ILogger<EfTemplateItemRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TemplateItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return record is null ? null : MapToDomain(record);
    }

    public async Task<IReadOnlyList<TemplateItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.Items
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return records.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task CreateAsync(TemplateItem item, CancellationToken cancellationToken = default)
    {
        var record = MapToRecord(item);
        _dbContext.Items.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted TemplateItem {Id} into [template].[Items]", item.Id);
    }

    public async Task UpdateAsync(TemplateItem item, CancellationToken cancellationToken = default)
    {
        var attributes = TemplateItemMapper.ToAttributes(item);
        var attributesJson = JsonSerializer.Serialize(attributes, SharedJsonOptions.CamelCase);

        var rowsAffected = await _dbContext.Items
            .Where(i => i.Id == item.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.Name, item.Name)
                .SetProperty(i => i.Status, item.Status)
                .SetProperty(i => i.Attributes, attributesJson)
                .SetProperty(i => i.UpdatedAt, item.UpdatedAt),
                cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for TemplateItem {Id}", item.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _dbContext.Items
            .Where(i => i.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
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

    private static TemplateItemRecord MapToRecord(TemplateItem item)
    {
        var attributes = TemplateItemMapper.ToAttributes(item);
        var attributesJson = JsonSerializer.Serialize(attributes, SharedJsonOptions.CamelCase);

        return new TemplateItemRecord
        {
            Id = item.Id,
            Name = item.Name,
            Status = item.Status,
            Attributes = attributesJson,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
