namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed record GetSsrOptionsResponse(IReadOnlyList<SsrOptionDto> SsrOptions);

public sealed record SsrOptionDto(string SsrCode, string Label, string Category);

public sealed record SsrOptionDetailDto(Guid SsrCatalogueId, string SsrCode, string Label, string Category, bool IsActive);
