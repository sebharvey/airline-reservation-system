using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

public sealed class AdminInventoryFunction
{
    private readonly OfferServiceClient _offerServiceClient;
    private readonly ILogger<AdminInventoryFunction> _logger;

    public AdminInventoryFunction(OfferServiceClient offerServiceClient, ILogger<AdminInventoryFunction> logger)
    {
        _offerServiceClient = offerServiceClient;
        _logger = logger;
    }

    [Function("AdminCancelInventory")]
    [OpenApiOperation(operationId: "AdminCancelInventory", tags: new[] { "Admin Inventory" }, Summary = "Cancel all inventory for a flight (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminCancelInventoryRequest), Required = true, Description = "Flight to cancel: flightNumber, departureDate")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Flight inventory cancelled")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Flight not found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Flight already cancelled")]
    public async Task<HttpResponseData> CancelInventory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/inventory/cancel")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminCancelInventoryRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.FlightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        if (string.IsNullOrWhiteSpace(request.DepartureDate) ||
            !DateOnly.TryParseExact(request.DepartureDate, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");

        try
        {
            await _offerServiceClient.CancelFlightInventoryAsync(
                request.FlightNumber, request.DepartureDate, cancellationToken);

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (KeyNotFoundException ex)
        {
            return await req.NotFoundAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return await req.UnprocessableEntityAsync(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel inventory for flight {FlightNumber} on {DepartureDate}",
                request.FlightNumber, request.DepartureDate);
            return await req.InternalServerErrorAsync();
        }
    }
}
