using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Application.GetSeatOffer;
using ReservationSystem.Microservices.Seat.Application.GetSeatOffers;

namespace ReservationSystem.Microservices.Seat.Functions;

/// <summary>
/// HTTP-triggered functions for Seat Offer resources.
/// Translates HTTP concerns into application-layer calls and back again.
/// </summary>
public sealed class SeatOfferFunction
{
    private readonly GetSeatOffersHandler _getSeatOffersHandler;
    private readonly GetSeatOfferHandler _getSeatOfferHandler;
    private readonly ILogger<SeatOfferFunction> _logger;

    public SeatOfferFunction(
        GetSeatOffersHandler getSeatOffersHandler,
        GetSeatOfferHandler getSeatOfferHandler,
        ILogger<SeatOfferFunction> logger)
    {
        _getSeatOffersHandler = getSeatOffersHandler;
        _getSeatOfferHandler = getSeatOfferHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/seat-offers?flightId={flightId}
    // -------------------------------------------------------------------------

    [Function("GetSeatOffers")]
    public async Task<HttpResponseData> GetSeatOffers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-offers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/seat-offers/{seatOfferId}
    // -------------------------------------------------------------------------

    [Function("GetSeatOffer")]
    public async Task<HttpResponseData> GetSeatOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-offers/{seatOfferId}")] HttpRequestData req,
        string seatOfferId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
