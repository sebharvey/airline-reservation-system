namespace ReservationSystem.Microservices.Delivery.Application.Watchlist.UpdateWatchlistEntry;

public sealed record UpdateWatchlistEntryCommand(
    Guid WatchlistId,
    string GivenName,
    string Surname,
    DateOnly DateOfBirth,
    string PassportNumber,
    string? Notes);
