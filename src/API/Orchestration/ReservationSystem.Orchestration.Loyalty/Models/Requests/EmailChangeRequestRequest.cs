namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class EmailChangeRequestRequest
{
    public string NewEmail { get; init; } = string.Empty;
}
