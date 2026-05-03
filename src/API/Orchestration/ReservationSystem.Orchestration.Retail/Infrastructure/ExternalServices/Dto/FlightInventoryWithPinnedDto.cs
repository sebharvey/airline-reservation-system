namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class FlightInventoryWithPinnedDto
{
    public IReadOnlyList<FlightInventoryGroupDto> Flights { get; init; } = [];
    public IReadOnlyList<FlightInventoryGroupDto> PinnedFlights { get; init; } = [];
}
