namespace ReservationSystem.Microservices.Delivery.Application.Watchlist.CreateWatchlistEntry;

public sealed record CreateWatchlistEntryCommand(
    string GivenName,
    string Surname,
    DateOnly DateOfBirth,
    string PassportNumber,
    string AddedBy,
    string? Notes);
