namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class RetrieveOrderRequest
{
    public string BookingReference { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
}
