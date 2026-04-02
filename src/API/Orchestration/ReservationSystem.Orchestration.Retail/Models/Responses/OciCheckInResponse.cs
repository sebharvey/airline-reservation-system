namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class OciCheckInResponse
{
    public IReadOnlyList<OciBoardingPass> BoardingPasses { get; init; } = [];
}

public sealed class OciBoardingPass
{
    public string BookingReference { get; init; } = string.Empty;
    public string PassengerId { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDateTime { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string ETicketNumber { get; init; } = string.Empty;
    public string SequenceNumber { get; init; } = string.Empty;
    public string BcbpBarcode { get; init; } = string.Empty;
    public string Gate { get; init; } = string.Empty;
    public string BoardingTime { get; init; } = string.Empty;
}
