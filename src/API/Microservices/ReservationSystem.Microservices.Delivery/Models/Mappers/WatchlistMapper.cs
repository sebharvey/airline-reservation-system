using ReservationSystem.Microservices.Delivery.Application.Watchlist.CreateWatchlistEntry;
using ReservationSystem.Microservices.Delivery.Application.Watchlist.UpdateWatchlistEntry;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Models.Mappers;

public static class WatchlistMapper
{
    public static WatchlistEntryResponse ToResponse(WatchlistEntry entry) => new()
    {
        WatchlistId = entry.WatchlistId,
        GivenName = entry.GivenName,
        Surname = entry.Surname,
        DateOfBirth = entry.DateOfBirth.ToString("yyyy-MM-dd"),
        PassportNumber = entry.PassportNumber,
        AddedBy = entry.AddedBy,
        Notes = entry.Notes,
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt,
    };

    public static IReadOnlyList<WatchlistEntryResponse> ToResponse(IReadOnlyList<WatchlistEntry> entries) =>
        entries.Select(ToResponse).ToList().AsReadOnly();

    public static CreateWatchlistEntryCommand ToCommand(CreateWatchlistEntryRequest request) =>
        new(
            GivenName: request.GivenName!,
            Surname: request.Surname!,
            DateOfBirth: DateOnly.Parse(request.DateOfBirth!),
            PassportNumber: request.PassportNumber!,
            AddedBy: request.AddedBy ?? "System",
            Notes: request.Notes);

    public static UpdateWatchlistEntryCommand ToCommand(Guid watchlistId, UpdateWatchlistEntryRequest request) =>
        new(
            WatchlistId: watchlistId,
            GivenName: request.GivenName!,
            Surname: request.Surname!,
            DateOfBirth: DateOnly.Parse(request.DateOfBirth!),
            PassportNumber: request.PassportNumber!,
            Notes: request.Notes);
}
