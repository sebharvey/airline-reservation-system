namespace ReservationSystem.Orchestration.Admin.Application.UpdateSsrOption;

public sealed record UpdateSsrOptionCommand(string SsrCode, string Label, string Category);
