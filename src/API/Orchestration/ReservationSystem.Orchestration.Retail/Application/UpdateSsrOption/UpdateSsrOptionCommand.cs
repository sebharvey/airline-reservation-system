namespace ReservationSystem.Orchestration.Retail.Application.UpdateSsrOption;

public sealed record UpdateSsrOptionCommand(string SsrCode, string Label, string Category);
