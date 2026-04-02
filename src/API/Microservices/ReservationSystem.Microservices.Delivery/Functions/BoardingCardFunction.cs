using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.CreateBoardingCards;
using ReservationSystem.Microservices.Delivery.Application.GetBoardingCardsByBooking;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class BoardingCardFunction
{
    private readonly CreateBoardingCardsHandler _checkInHandler;
    private readonly GetBoardingCardsByBookingHandler _getByBookingHandler;
    private readonly ILogger<BoardingCardFunction> _logger;

    public BoardingCardFunction(
        CreateBoardingCardsHandler checkInHandler,
        GetBoardingCardsByBookingHandler getByBookingHandler,
        ILogger<BoardingCardFunction> logger)
    {
        _checkInHandler = checkInHandler;
        _getByBookingHandler = getByBookingHandler;
        _logger = logger;
    }

    // POST /v1/checkin
    // Performs check-in: updates ticket coupon statuses and marks manifest entries
    // as checked-in for the supplied passengers and inventory IDs.
    [Function("DeliveryCheckIn")]
    [OpenApiOperation(operationId: "DeliveryCheckIn", tags: new[] { "BoardingCards" }, Summary = "Check in passengers — updates ticket coupon statuses and manifest check-in flags")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBoardingCardsRequest), Required = true, Description = "Check-in request: bookingReference, passengers with inventoryIds")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CreateBoardingCardsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/checkin")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateBoardingCardsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("The 'bookingReference' field is required.");

        if (request.Passengers.Count == 0)
            return await req.BadRequestAsync("At least one passenger is required.");

        try
        {
            var result = await _checkInHandler.HandleAsync(request, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check in passengers for booking {BookingRef}", request.BookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // POST /v1/boarding-cards
    // Returns boarding cards for all checked-in passengers on a booking,
    // reading from the delivery.Ticket and delivery.Manifest tables.
    [Function("GetBoardingCards")]
    [OpenApiOperation(operationId: "GetBoardingCards", tags: new[] { "BoardingCards" }, Summary = "Retrieve boarding cards for all checked-in passengers on a booking")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(GetBoardingCardsRequest), Required = true, Description = "Boarding card retrieval request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CreateBoardingCardsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request — bookingReference missing")]
    public async Task<HttpResponseData> GetBoardingCards(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/boarding-cards")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<GetBoardingCardsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("The 'bookingReference' field is required.");

        var result = await _getByBookingHandler.HandleAsync(
            request.BookingReference.ToUpperInvariant().Trim(), cancellationToken);
        return await req.OkJsonAsync(result);
    }
}
