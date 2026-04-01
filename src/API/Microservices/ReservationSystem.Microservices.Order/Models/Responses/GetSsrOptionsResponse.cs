namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed record GetSsrOptionsResponse(IReadOnlyList<SsrOptionDto> SsrOptions);

public sealed record SsrOptionDto(string SsrCode, string Label, string Category);
