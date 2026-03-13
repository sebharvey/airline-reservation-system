using ReservationSystem.Template.Api.Application.UseCases.CreateTemplateItem;
using ReservationSystem.Template.Api.Domain.Entities;
using ReservationSystem.Template.Api.Domain.ValueObjects;
using ReservationSystem.Template.Api.Models.Database;
using ReservationSystem.Template.Api.Models.Database.JsonFields;
using ReservationSystem.Template.Api.Models.Requests;
using ReservationSystem.Template.Api.Models.Responses;

namespace ReservationSystem.Template.Api.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of a TemplateItem.
///
/// Mapping directions:
///
///   HTTP request  →  Application command
///   DB record + JSON field  →  Domain entity
///   Domain entity  →  HTTP response
///   Domain entity  →  JSON field (for DB write)
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class TemplateItemMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateTemplateItemCommand ToCommand(CreateTemplateItemRequest request) =>
        new(
            Name: request.Name,
            Tags: request.Tags.AsReadOnly(),
            Priority: request.Priority,
            Properties: request.Properties.AsReadOnly());

    // -------------------------------------------------------------------------
    // DB record + JSON field → Domain entity
    // -------------------------------------------------------------------------

    public static TemplateItem ToDomain(TemplateItemRecord record, TemplateItemAttributes? attributes)
    {
        var metadata = attributes is null
            ? TemplateItemMetadata.Empty
            : new TemplateItemMetadata(
                attributes.Tags.AsReadOnly(),
                attributes.Priority,
                attributes.Properties.AsReadOnly());

        return TemplateItem.Reconstitute(
            record.Id,
            record.Name,
            record.Status,
            metadata,
            record.CreatedAt,
            record.UpdatedAt);
    }

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static TemplateItemResponse ToResponse(TemplateItem item) =>
        new()
        {
            Id = item.Id,
            Name = item.Name,
            Status = item.Status,
            Metadata = new TemplateItemMetadataResponse
            {
                Tags = [.. item.Metadata.Tags],
                Priority = item.Metadata.Priority,
                Properties = new Dictionary<string, string>(item.Metadata.Properties)
            },
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };

    public static IReadOnlyList<TemplateItemResponse> ToResponse(IEnumerable<TemplateItem> items) =>
        items.Select(ToResponse).ToList().AsReadOnly();

    // -------------------------------------------------------------------------
    // Domain entity → JSON field (for DB write)
    // -------------------------------------------------------------------------

    public static TemplateItemAttributes ToAttributes(TemplateItem item) =>
        new()
        {
            Tags = [.. item.Metadata.Tags],
            Priority = item.Metadata.Priority,
            Properties = new Dictionary<string, string>(item.Metadata.Properties)
        };
}
