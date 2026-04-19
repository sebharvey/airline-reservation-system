using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Domain.ExternalServices;

public interface IRetailApiClient
{
    Task<SearchSliceResponse> SearchSliceAsync(SearchSliceRequest request, CancellationToken ct = default);

    Task<CreateBasketResponse> CreateBasketAsync(CreateBasketRequest request, CancellationToken ct = default);

    Task AddPassengersAsync(string basketId, List<PassengerRequest> passengers, CancellationToken ct = default);

    Task GetBasketSummaryAsync(string basketId, CancellationToken ct = default);

    Task<GetBasketResponse> GetBasketAsync(string basketId, CancellationToken ct = default);

    Task<GetSeatmapResponse> GetSeatmapAsync(string inventoryId, string aircraftType, string flightNumber, string cabinCode, CancellationToken ct = default);

    Task AddSeatsAsync(string basketId, List<SeatAssignment> seats, CancellationToken ct = default);

    Task AddSsrsAsync(string basketId, List<SsrRequest> ssrs, CancellationToken ct = default);

    Task<GetBagOffersResponse> GetBagOffersAsync(string inventoryId, string cabinCode, CancellationToken ct = default);

    Task AddBagsAsync(string basketId, List<BagSelection> bags, CancellationToken ct = default);

    Task<GetProductsResponse> GetProductsAsync(CancellationToken ct = default);

    Task AddProductsAsync(string basketId, List<ProductSelection> products, CancellationToken ct = default);

    Task<ConfirmBasketResponse> ConfirmBasketAsync(string basketId, ConfirmBasketRequest request, CancellationToken ct = default);
}
