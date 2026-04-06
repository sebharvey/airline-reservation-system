namespace ReservationSystem.Microservices.Offer.Domain.Repositories;

public interface IOfferRepository
{
    // FlightInventory
    Task<int> DeleteExpiredFlightInventoryAsync(CancellationToken ct = default);
    Task<Entities.FlightInventory?> GetInventoryByIdAsync(Guid inventoryId, CancellationToken ct = default);
    Task<Entities.FlightInventory?> GetInventoryAsync(string flightNumber, DateOnly departureDate, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FlightInventory>> SearchInventoryAsync(string origin, string destination, DateOnly departureDate, string cabinCode, int paxCount, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FlightInventory>> SearchAvailableInventoryAsync(string origin, string destination, DateOnly departureDate, int paxCount, CancellationToken ct = default);
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
    Task BatchCreateFaresAsync(IReadOnlyList<Entities.Fare> fares, CancellationToken ct = default);

    // StoredOffer
    Task<int> DeleteExpiredStoredOffersAsync(CancellationToken ct = default);
    Task<Entities.StoredOffer?> GetStoredOfferByOfferIdAsync(Guid offerId, CancellationToken ct = default);
    Task<Entities.StoredOffer?> GetStoredOfferBySessionAndOfferIdAsync(Guid sessionId, Guid offerId, CancellationToken ct = default);
    Task CreateStoredOfferAsync(Entities.StoredOffer offer, CancellationToken ct = default);

    // InventoryHold (idempotency)
    Task<bool> HoldExistsAsync(Guid inventoryId, Guid orderId, CancellationToken ct = default);
    Task CreateHoldAsync(Guid inventoryId, Guid orderId, int paxCount, CancellationToken ct = default);
    Task ConfirmHoldAsync(Guid inventoryId, Guid orderId, CancellationToken ct = default);

    // SeatReservation
    Task<IReadOnlyList<(string SeatNumber, string Status, Guid BasketId)>> GetSeatReservationsAsync(Guid inventoryId, CancellationToken ct = default);
    Task CreateSeatReservationsAsync(Guid inventoryId, Guid basketId, IEnumerable<string> seatNumbers, CancellationToken ct = default);
    Task UpdateSeatStatusAsync(Guid inventoryId, string seatNumber, string status, CancellationToken ct = default);

    // FareRule
    Task<Entities.FareRule?> GetFareRuleByIdAsync(Guid fareRuleId, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FareRule>> GetAllFareRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FareRule>> SearchFareRulesAsync(string? query, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FareRule>> GetApplicableFareRulesAsync(string flightNumber, string cabinCode, DateOnly departureDate, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.FareRule>> GetApplicableFareRulesForFlightsAsync(IReadOnlyList<string> flightNumbers, IReadOnlyList<string> cabinCodes, DateOnly departureDate, CancellationToken ct = default);
    Task CreateFareRuleAsync(Entities.FareRule fareRule, CancellationToken ct = default);
    Task UpdateFareRuleAsync(Entities.FareRule fareRule, CancellationToken ct = default);
    Task<bool> DeleteFareRuleAsync(Guid fareRuleId, CancellationToken ct = default);
}
