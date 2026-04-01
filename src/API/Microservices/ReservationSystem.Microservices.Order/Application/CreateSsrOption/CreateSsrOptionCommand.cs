namespace ReservationSystem.Microservices.Order.Application.CreateSsrOption;

public sealed record CreateSsrOptionCommand(string SsrCode, string Label, string Category);
