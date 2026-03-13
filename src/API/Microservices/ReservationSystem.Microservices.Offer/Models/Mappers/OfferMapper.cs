using ReservationSystem.Microservices.Offer.Application.UseCases.CreateOffer;
using Offer = ReservationSystem.Microservices.Offer.Domain.Entities.Offer;
using ReservationSystem.Microservices.Offer.Domain.ValueObjects;
using ReservationSystem.Microservices.Offer.Models.Database;
using ReservationSystem.Microservices.Offer.Models.Database.JsonFields;
using ReservationSystem.Microservices.Offer.Models.Requests;
using ReservationSystem.Microservices.Offer.Models.Responses;

namespace ReservationSystem.Microservices.Offer.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of an Offer.
///
/// Mapping directions:
///
///   HTTP request  →  Application command
///   DB record + JSON field  →  Domain entity
///   Domain entity  →  HTTP response
///   Domain entity  →  JSON field (for DB write)
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class OfferMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateOfferCommand ToCommand(CreateOfferRequest request) =>
        new(
            FlightNumber: request.FlightNumber,
            Origin: request.Origin,
            Destination: request.Destination,
            DepartureAt: request.DepartureAt,
            FareClass: request.FareClass,
            TotalPrice: request.TotalPrice,
            Currency: request.Currency,
            BaggageAllowance: request.BaggageAllowance,
            IsRefundable: request.IsRefundable,
            IsChangeable: request.IsChangeable,
            SeatsRemaining: request.SeatsRemaining);

    // -------------------------------------------------------------------------
    // DB record + JSON field → Domain entity
    // -------------------------------------------------------------------------

    public static Offer ToDomain(OfferRecord record, OfferAttributes? attributes)
    {
        var metadata = attributes is null
            ? OfferMetadata.Empty
            : new OfferMetadata(
                attributes.BaggageAllowance,
                attributes.IsRefundable,
                attributes.IsChangeable,
                attributes.SeatsRemaining);

        return Offer.Reconstitute(
            record.Id,
            record.FlightNumber,
            record.Origin,
            record.Destination,
            record.DepartureAt,
            record.FareClass,
            record.TotalPrice,
            record.Currency,
            record.Status,
            metadata,
            record.CreatedAt,
            record.UpdatedAt);
    }

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static OfferResponse ToResponse(Offer offer) =>
        new()
        {
            Id = offer.Id,
            FlightNumber = offer.FlightNumber,
            Origin = offer.Origin,
            Destination = offer.Destination,
            DepartureAt = offer.DepartureAt,
            FareClass = offer.FareClass,
            TotalPrice = offer.TotalPrice,
            Currency = offer.Currency,
            Status = offer.Status,
            Metadata = new OfferMetadataResponse
            {
                BaggageAllowance = offer.Metadata.BaggageAllowance,
                IsRefundable = offer.Metadata.IsRefundable,
                IsChangeable = offer.Metadata.IsChangeable,
                SeatsRemaining = offer.Metadata.SeatsRemaining
            },
            CreatedAt = offer.CreatedAt,
            UpdatedAt = offer.UpdatedAt
        };

    public static IReadOnlyList<OfferResponse> ToResponse(IEnumerable<Offer> offers) =>
        offers.Select(ToResponse).ToList().AsReadOnly();

    // -------------------------------------------------------------------------
    // Domain entity → JSON field (for DB write)
    // -------------------------------------------------------------------------

    public static OfferAttributes ToAttributes(Offer offer) =>
        new()
        {
            BaggageAllowance = offer.Metadata.BaggageAllowance,
            IsRefundable = offer.Metadata.IsRefundable,
            IsChangeable = offer.Metadata.IsChangeable,
            SeatsRemaining = offer.Metadata.SeatsRemaining
        };
}
