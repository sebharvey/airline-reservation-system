namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class AdminSetAccountStatusRequest
{
    public bool IsActive { get; init; }
}
