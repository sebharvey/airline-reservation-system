using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Delivery.Application.CreateBoardingCards;
using ReservationSystem.Microservices.Delivery.Application.GetBoardingCardsByBooking;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class BoardingCardFunction
{
    private readonly CreateBoardingCardsHandler _createHandler;
    private readonly GetBoardingCardsByBookingHandler _getByBookingHandler;
    private readonly ILogger<BoardingCardFunction> _logger;

    public BoardingCardFunction(
        CreateBoardingCardsHandler createHandler,
        GetBoardingCardsByBookingHandler getByBookingHandler,
        ILogger<BoardingCardFunction> logger)
    {
        _createHandler = createHandler;
        _getByBookingHandler = getByBookingHandler;
        _logger = logger;
    }

    // POST /v1/boarding-cards
    [Function("CreateBoardingCards")]
    [OpenApiOperation(operationId: "CreateBoardingCards", tags: new[] { "BoardingCards" }, Summary = "Create boarding cards for passengers")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBoardingCardsRequest), Required = true, Description = "Boarding card creation request: bookingReference, passengers")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateBoardingCardsResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateBoardingCards(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/boarding-cards")] HttpRequestData req,
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
            var result = await _createHandler.HandleAsync(request, cancellationToken);
            return await req.CreatedAsync("/v1/boarding-cards", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create boarding cards for booking {BookingRef}", request.BookingReference);
            return await req.InternalServerErrorAsync();
        }
    }

    // GET /v1/boarding-cards?bookingRef=ABC123
    [Function("GetBoardingCardsByBooking")]
    [OpenApiOperation(operationId: "GetBoardingCardsByBooking", tags: new[] { "BoardingCards" }, Summary = "Retrieve boarding cards for all checked-in passengers on a booking")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CreateBoardingCardsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request — bookingRef missing")]
    public async Task<HttpResponseData> GetBoardingCardsByBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/boarding-cards")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var bookingRef = query["bookingRef"];

        if (string.IsNullOrWhiteSpace(bookingRef))
            return await req.BadRequestAsync("The 'bookingRef' query parameter is required.");

        var result = await _getByBookingHandler.HandleAsync(bookingRef.ToUpperInvariant().Trim(), cancellationToken);
        return await req.OkJsonAsync(result);
    }
}
