using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.SearchOffers;

public sealed class SearchOffersHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<SearchOffersHandler> _logger;

    public SearchOffersHandler(IOfferRepository repository, ILogger<SearchOffersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoredOffer>> HandleAsync(SearchOffersCommand command, CancellationToken ct = default)
    {
        var departureDate = DateOnly.Parse(command.DepartureDate);
        var bookingType = string.IsNullOrEmpty(command.BookingType) ? "Revenue" : command.BookingType;

        var inventories = await _repository.SearchInventoryAsync(
            command.Origin, command.Destination, departureDate, command.CabinCode, command.PaxCount, ct);

        var offers = new List<StoredOffer>();

        foreach (var inventory in inventories)
        {
            var fares = await _repository.GetActiveFaresByInventoryAsync(inventory.InventoryId, ct);

            foreach (var fare in fares)
            {
                if (bookingType == "Reward" && fare.PointsPrice is null)
                    continue;

                var storedOffer = StoredOffer.Create(inventory, fare, bookingType);
                await _repository.CreateStoredOfferAsync(storedOffer, ct);
                offers.Add(storedOffer);
            }
        }

        _logger.LogInformation("Search {Origin}-{Destination} on {Date} cabin {Cabin}: {Count} offers created",
            command.Origin, command.Destination, command.DepartureDate, command.CabinCode, offers.Count);

        return offers.AsReadOnly();
    }
}
