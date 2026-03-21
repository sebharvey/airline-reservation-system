using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.CreateManifest;

public sealed class CreateManifestHandler
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ILogger<CreateManifestHandler> _logger;

    public CreateManifestHandler(IManifestRepository manifestRepository, ILogger<CreateManifestHandler> logger)
    {
        _manifestRepository = manifestRepository;
        _logger = logger;
    }

    public async Task<CreateManifestResponse> HandleAsync(CreateManifestRequest request, CancellationToken cancellationToken = default)
    {
        var entries = new List<ManifestEntrySummary>();

        foreach (var entry in request.Entries)
        {
            var departureDate = DateTime.Parse(entry.DepartureDate);
            var departureTime = TimeSpan.Parse(entry.DepartureTime);
            var arrivalTime = TimeSpan.Parse(entry.ArrivalTime);

            var ssrCodesJson = entry.SsrCodes is not null && entry.SsrCodes.Count > 0
                ? JsonSerializer.Serialize(entry.SsrCodes, SharedJsonOptions.CamelCase)
                : "[]";

            var manifest = Manifest.Create(
                entry.TicketId, entry.InventoryId, entry.FlightNumber, departureDate,
                entry.AircraftType, entry.SeatNumber, entry.CabinCode,
                request.BookingReference, entry.ETicketNumber,
                entry.PassengerId, entry.GivenName, entry.Surname,
                ssrCodesJson, departureTime, arrivalTime);

            await _manifestRepository.CreateAsync(manifest, cancellationToken);
            entries.Add(DeliveryMapper.ToManifestSummary(manifest));

            _logger.LogInformation("Created manifest entry {ManifestId} for {PassengerId} on {FlightNumber}",
                manifest.ManifestId, entry.PassengerId, entry.FlightNumber);
        }

        return new CreateManifestResponse { ManifestEntries = entries };
    }
}
