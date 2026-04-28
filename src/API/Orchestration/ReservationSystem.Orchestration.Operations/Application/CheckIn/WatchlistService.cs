using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.CheckIn;

public sealed record WatchlistMatch(
    string PassengerId,
    string TicketNumber,
    string GivenName,
    string Surname,
    string PassportNumber,
    string? Notes);

/// <summary>
/// Thrown when check-in is blocked because one or more passengers are on the security watchlist
/// and no override has been authorised.
/// </summary>
public sealed class OciWatchlistBlockedException : Exception
{
    public IReadOnlyList<WatchlistMatch> Matches { get; }

    public OciWatchlistBlockedException(string message, IReadOnlyList<WatchlistMatch> matches)
        : base(message) => Matches = matches;
}

/// <summary>
/// Checks passengers against the security watchlist.
/// A passenger matches if their passport number equals a watchlist entry's passport number,
/// OR if their given name, surname, and date of birth all match a watchlist entry.
/// If the watchlist cannot be reached the check is skipped and check-in proceeds (fail-open).
/// </summary>
public sealed class WatchlistService
{
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(DeliveryServiceClient deliveryServiceClient, ILogger<WatchlistService> logger)
    {
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WatchlistMatch>> CheckAsync(
        IEnumerable<(string PassengerId, string TicketNumber, string GivenName, string Surname, string? PassportNumber, string? Dob)> passengers,
        CancellationToken ct)
    {
        IReadOnlyList<Infrastructure.ExternalServices.Dto.WatchlistEntryDto> entries;
        try
        {
            entries = await _deliveryServiceClient.GetAllWatchlistEntriesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Watchlist check: unable to fetch watchlist entries; proceeding without check");
            return [];
        }

        if (entries.Count == 0) return [];

        var matches = new List<WatchlistMatch>();
        foreach (var pax in passengers)
        {
            var entry = FindMatch(entries, pax.GivenName, pax.Surname, pax.Dob, pax.PassportNumber);
            if (entry is null) continue;

            _logger.LogWarning(
                "Watchlist match: passenger {PassengerId} ticket {TicketNumber}",
                pax.PassengerId, pax.TicketNumber);

            matches.Add(new WatchlistMatch(
                pax.PassengerId, pax.TicketNumber,
                pax.GivenName, pax.Surname,
                pax.PassportNumber ?? string.Empty, entry.Notes));
        }

        return matches;
    }

    private static Infrastructure.ExternalServices.Dto.WatchlistEntryDto? FindMatch(
        IReadOnlyList<Infrastructure.ExternalServices.Dto.WatchlistEntryDto> entries,
        string givenName,
        string surname,
        string? dob,
        string? passportNumber)
    {
        foreach (var entry in entries)
        {
            // Match on passport number
            if (!string.IsNullOrWhiteSpace(passportNumber) &&
                string.Equals(entry.PassportNumber, passportNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                return entry;

            // Match on given name + surname + date of birth
            if (!string.IsNullOrWhiteSpace(dob) &&
                DateOnly.TryParse(dob, out var paxDob) &&
                DateOnly.TryParse(entry.DateOfBirth, out var entryDob) &&
                paxDob == entryDob &&
                string.Equals(entry.GivenName, givenName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Surname, surname.Trim(), StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }
}
