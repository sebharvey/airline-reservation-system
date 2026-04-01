namespace ReservationSystem.Microservices.Order.Application.UpdateSsrOption;

public sealed record UpdateSsrOptionCommand(string SsrCode, string Label, string Category);
