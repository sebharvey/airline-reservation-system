using ReservationSystem.Microservices.Simulator.Models;

namespace ReservationSystem.Microservices.Simulator.Domain.ExternalServices;

internal interface IRetailApiClient
{
    Task<SearchSliceResponse> SearchSliceAsync(SearchSliceRequest request, CancellationToken ct = default);

    Task<CreateBasketResponse> CreateBasketAsync(CreateBasketRequest request, CancellationToken ct = default);

    Task AddPassengersAsync(string basketId, List<PassengerRequest> passengers, CancellationToken ct = default);

    Task<GetBasketResponse> GetBasketAsync(string basketId, CancellationToken ct = default);

    Task<GetSeatmapResponse> GetSeatmapAsync(string inventoryId, string aircraftType, string flightNumber, string cabinCode, CancellationToken ct = default);

    Task AddSeatsAsync(string basketId, List<SeatAssignment> seats, CancellationToken ct = default);

    Task<ConfirmBasketResponse> ConfirmBasketAsync(string basketId, ConfirmBasketRequest request, CancellationToken ct = default);
}
