namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class CreateFlightDto
{
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public int SeatsAvailable { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<CabinDto> Cabins { get; init; } = [];
}

public sealed class CabinDto
{
    public string CabinCode { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public int SeatsAvailable { get; init; }
}

public sealed class CreateFareDto
{
    public Guid FareId { get; init; }
    public Guid InventoryId { get; init; }
    public string FareBasisCode { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}

/// <summary>
/// DTO for the Offer MS POST /v1/flights/batch response.
/// </summary>
public sealed class BatchCreateFlightsResultDto
{
    public int Created { get; init; }
    public int Skipped { get; init; }
    public IReadOnlyList<CreateFlightDto> Inventories { get; init; } = [];
}
