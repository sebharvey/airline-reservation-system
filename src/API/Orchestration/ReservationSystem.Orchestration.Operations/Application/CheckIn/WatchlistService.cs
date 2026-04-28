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
/// Checks passengers against the security watchlist by passport number.
/// A match failure is non-fatal to note recording — if the watchlist cannot be reached
/// the check is skipped and check-in proceeds (fail-open), logged as a warning.
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
        IEnumerable<(string PassengerId, string TicketNumber, string GivenName, string Surname, string? PassportNumber)> passengers,
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
            if (string.IsNullOrWhiteSpace(pax.PassportNumber)) continue;

            var passportNormalized = pax.PassportNumber.Trim().ToUpperInvariant();
            var entry = entries.FirstOrDefault(e =>
                string.Equals(e.PassportNumber, passportNormalized, StringComparison.OrdinalIgnoreCase));

            if (entry is null) continue;

            _logger.LogWarning(
                "Watchlist match: passenger {PassengerId} ticket {TicketNumber} passport {PassportNumber}",
                pax.PassengerId, pax.TicketNumber, passportNormalized);

            matches.Add(new WatchlistMatch(
                pax.PassengerId, pax.TicketNumber,
                pax.GivenName, pax.Surname,
                passportNormalized, entry.Notes));
        }

        return matches;
    }
}
