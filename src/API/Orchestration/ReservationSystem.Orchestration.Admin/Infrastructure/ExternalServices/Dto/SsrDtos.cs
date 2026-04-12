namespace ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices.Dto;

// Internal DTOs for deserialising Order microservice SSR responses.
// These are not exposed beyond the infrastructure layer.

public sealed class SsrOptionListMsResponse
{
    public IReadOnlyList<SsrOptionMsDto> SsrOptions { get; init; } = [];
}

public sealed class SsrOptionMsDto
{
    public string SsrCode { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}

public sealed class SsrOptionDetailMsResponse
{
    public Guid SsrCatalogueId { get; init; }
    public string SsrCode { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
