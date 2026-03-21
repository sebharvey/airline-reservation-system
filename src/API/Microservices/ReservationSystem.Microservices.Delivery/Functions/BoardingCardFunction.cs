using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.CreateBoardingCards;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class BoardingCardFunction
{
    private readonly CreateBoardingCardsHandler _createHandler;
    private readonly ILogger<BoardingCardFunction> _logger;

    public BoardingCardFunction(
        CreateBoardingCardsHandler createHandler,
        ILogger<BoardingCardFunction> logger)
    {
        _createHandler = createHandler;
        _logger = logger;
    }

    // POST /v1/boarding-cards
    [Function("CreateBoardingCards")]
    public async Task<HttpResponseData> CreateBoardingCards(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/boarding-cards")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateBoardingCardsRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateBoardingCardsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateBoardingCards request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("The 'bookingReference' field is required.");

        if (request.Passengers.Count == 0)
            return await req.BadRequestAsync("At least one passenger is required.");

        try
        {
            var result = await _createHandler.HandleAsync(request, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create boarding cards for booking {BookingRef}", request.BookingReference);
            return await req.InternalServerErrorAsync();
        }
    }
}
