namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class CreateFlightDto
{
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public int SeatsAvailable { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class CreateFareDto
{
    public Guid FareId { get; init; }
    public Guid InventoryId { get; init; }
    public string FareBasisCode { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}
