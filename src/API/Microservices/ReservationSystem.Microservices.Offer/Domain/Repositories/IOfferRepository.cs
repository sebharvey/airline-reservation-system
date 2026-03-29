namespace ReservationSystem.Microservices.Offer.Domain.Repositories;

public interface IOfferRepository
{
    // FlightInventory
    Task<Entities.FlightInventory?> GetInventoryByIdAsync(Guid inventoryId, CancellationToken ct = default);
    Task<Entities.FlightInventory?> GetInventoryAsync(string flightNumber, DateOnly departureDate, string cabinCode, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FlightInventory>> SearchInventoryAsync(string origin, string destination, DateOnly departureDate, string cabinCode, int paxCount, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FlightInventory>> GetInventoriesByFlightAsync(string flightNumber, DateOnly departureDate, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FlightInventoryGroup>> GetInventoryGroupedByDateAsync(DateOnly departureDate, CancellationToken ct = default);
    Task CreateInventoryAsync(Entities.FlightInventory inventory, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FlightInventory>> BatchCreateInventoryAsync(IReadOnlyList<Entities.FlightInventory> inventories, CancellationToken ct = default);
    Task UpdateInventoryAsync(Entities.FlightInventory inventory, CancellationToken ct = default);

    // Fare
    Task<Entities.Fare?> GetFareByIdAsync(Guid fareId, CancellationToken ct = default);
    Task<Entities.Fare?> GetFareAsync(Guid inventoryId, string fareBasisCode, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.Fare>> GetFaresByInventoryAsync(Guid inventoryId, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.Fare>> GetActiveFaresByInventoryAsync(Guid inventoryId, CancellationToken ct = default);
    Task CreateFareAsync(Entities.Fare fare, CancellationToken ct = default);

    // StoredOffer
    Task<Entities.StoredOffer?> GetStoredOfferAsync(Guid offerId, CancellationToken ct = default);
    Task CreateStoredOfferAsync(Entities.StoredOffer offer, CancellationToken ct = default);
    Task UpdateStoredOfferAsync(Entities.StoredOffer offer, CancellationToken ct = default);

    // InventoryHold (idempotency)
    Task<bool> HoldExistsAsync(Guid inventoryId, Guid basketId, CancellationToken ct = default);
    Task CreateHoldAsync(Guid inventoryId, Guid basketId, int paxCount, CancellationToken ct = default);

    // SeatReservation
    Task<IReadOnlyList<(string SeatNumber, string Status, Guid BasketId)>> GetSeatReservationsAsync(Guid inventoryId, CancellationToken ct = default);
    Task CreateSeatReservationsAsync(Guid inventoryId, Guid basketId, IEnumerable<string> seatNumbers, CancellationToken ct = default);
    Task UpdateSeatStatusAsync(Guid inventoryId, string seatNumber, string status, CancellationToken ct = default);

    // FareRule
    Task<Entities.FareRule?> GetFareRuleByIdAsync(Guid fareRuleId, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FareRule>> GetAllFareRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FareRule>> SearchFareRulesAsync(string? query, CancellationToken ct = default);
    Task CreateFareRuleAsync(Entities.FareRule fareRule, CancellationToken ct = default);
    Task UpdateFareRuleAsync(Entities.FareRule fareRule, CancellationToken ct = default);
    Task<bool> DeleteFareRuleAsync(Guid fareRuleId, CancellationToken ct = default);
}
