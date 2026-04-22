using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionCancel;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionChange;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionGetOrders;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionRebookOrder;
using ReservationSystem.Orchestration.Operations.Application.AdminDisruptionTime;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using ReservationSystem.Orchestration.Operations.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

public sealed class AdminDisruptionFunction
{
    private readonly AdminDisruptionCancelHandler _cancelHandler;
    private readonly AdminDisruptionChangeHandler _changeHandler;
    private readonly AdminDisruptionTimeHandler _timeHandler;
    private readonly AdminDisruptionGetOrdersHandler _getOrdersHandler;
    private readonly AdminDisruptionRebookOrderHandler _rebookOrderHandler;
    private readonly ILogger<AdminDisruptionFunction> _logger;

    public AdminDisruptionFunction(
        AdminDisruptionCancelHandler cancelHandler,
        AdminDisruptionChangeHandler changeHandler,
        AdminDisruptionTimeHandler timeHandler,
        AdminDisruptionGetOrdersHandler getOrdersHandler,
        AdminDisruptionRebookOrderHandler rebookOrderHandler,
        ILogger<AdminDisruptionFunction> logger)
    {
        _cancelHandler = cancelHandler;
        _changeHandler = changeHandler;
        _timeHandler = timeHandler;
        _getOrdersHandler = getOrdersHandler;
        _rebookOrderHandler = rebookOrderHandler;
        _logger = logger;
    }

    [Function("AdminDisruptionCancel")]
    [OpenApiOperation(operationId: "AdminDisruptionCancel", tags: new[] { "Admin Disruption" }, Summary = "Cancel a flight and synchronously rebook all affected passengers (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminDisruptionCancelRequest), Required = true, Description = "Flight to cancel: flightNumber, departureDate, optional reason")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminDisruptionCancelResponse), Description = "IROPS processing complete with per-passenger outcomes")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Flight not found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Flight already cancelled")]
    public async Task<HttpResponseData> CancelFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/disruption/cancel")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminDisruptionCancelRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.FlightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        if (string.IsNullOrWhiteSpace(request.DepartureDate) ||
            !DateOnly.TryParseExact(request.DepartureDate, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");

        try
        {
            var command = new AdminDisruptionCancelCommand(
                request.FlightNumber,
                request.DepartureDate,
                request.Reason);

            var result = await _cancelHandler.HandleAsync(command, cancellationToken);

            return await req.OkJsonAsync(result);
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
            _logger.LogError(ex, "IROPS cancellation failed for flight {FlightNumber} on {DepartureDate}",
                request.FlightNumber, request.DepartureDate);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDisruptionGetOrders")]
    [OpenApiOperation(operationId: "AdminDisruptionGetOrders", tags: new[] { "Admin Disruption" }, Summary = "Get affected orders for a cancelled flight in IROPS rebooking priority order (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminDisruptionOrdersResponse), Description = "Orders sorted by cabin, loyalty tier, and booking date")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Flight not found")]
    public async Task<HttpResponseData> GetDisruptionOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/disruption/orders")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var flightNumber = qs["flightNumber"];
        var departureDate = qs["departureDate"];

        if (string.IsNullOrWhiteSpace(flightNumber))
            return await req.BadRequestAsync("'flightNumber' query parameter is required.");

        if (string.IsNullOrWhiteSpace(departureDate) ||
            !DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");

        try
        {
            var query = new AdminDisruptionGetOrdersQuery(flightNumber, departureDate);
            var result = await _getOrdersHandler.HandleAsync(query, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException ex)
        {
            return await req.NotFoundAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve disruption orders for flight {FlightNumber} on {DepartureDate}",
                flightNumber, departureDate);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDisruptionRebookOrder")]
    [OpenApiOperation(operationId: "AdminDisruptionRebookOrder", tags: new[] { "Admin Disruption" }, Summary = "Rebook a single order affected by a flight cancellation (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminDisruptionRebookOrderRequest), Required = true, Description = "bookingReference, flightNumber, departureDate")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminDisruptionRebookOrderResponse), Description = "Rebook outcome for the specified booking")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Flight or booking not found")]
    public async Task<HttpResponseData> RebookOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/disruption/rebook-order")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminDisruptionRebookOrderRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.BookingReference))
            return await req.BadRequestAsync("'bookingReference' is required.");

        if (string.IsNullOrWhiteSpace(request.FlightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        if (string.IsNullOrWhiteSpace(request.DepartureDate) ||
            !DateOnly.TryParseExact(request.DepartureDate, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");

        try
        {
            var command = new AdminDisruptionRebookOrderCommand(
                request.BookingReference,
                request.FlightNumber,
                request.DepartureDate,
                request.Reason);

            var result = await _rebookOrderHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
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
            _logger.LogError(ex, "IROPS rebook failed for booking {BookingRef} on flight {FlightNumber}/{DepartureDate}",
                request.BookingReference, request.FlightNumber, request.DepartureDate);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDisruptionChange")]
    [OpenApiOperation(operationId: "AdminDisruptionChange", tags: new[] { "Admin Disruption" }, Summary = "Handle aircraft type change disruption (staff) — not yet implemented")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminDisruptionChangeRequest), Required = true, Description = "Flight and new aircraft type")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminDisruptionChangeResponse), Description = "Aircraft change processed")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotImplemented, Description = "Not yet implemented")]
    public async Task<HttpResponseData> ChangeFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/disruption/change")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminDisruptionChangeRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.FlightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        if (string.IsNullOrWhiteSpace(request.DepartureDate) ||
            !DateOnly.TryParseExact(request.DepartureDate, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");

        if (string.IsNullOrWhiteSpace(request.NewAircraftType))
            return await req.BadRequestAsync("'newAircraftType' is required.");

        try
        {
            var command = new AdminDisruptionChangeCommand(
                request.FlightNumber,
                request.DepartureDate,
                request.NewAircraftType,
                request.Reason);

            var result = await _changeHandler.HandleAsync(command, cancellationToken);

            return await req.OkJsonAsync(result);
        }
        catch (NotImplementedException)
        {
            return req.CreateResponse(HttpStatusCode.NotImplemented);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aircraft change disruption failed for flight {FlightNumber} on {DepartureDate}",
                request.FlightNumber, request.DepartureDate);
            return await req.InternalServerErrorAsync();
        }
    }

    [Function("AdminDisruptionTime")]
    [OpenApiOperation(operationId: "AdminDisruptionTime", tags: new[] { "Admin Disruption" }, Summary = "Handle flight time change disruption (staff) — not yet implemented")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminDisruptionTimeRequest), Required = true, Description = "Flight and new departure/arrival times")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminDisruptionTimeResponse), Description = "Time change processed")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotImplemented, Description = "Not yet implemented")]
    public async Task<HttpResponseData> ChangeFlightTime(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/disruption/time")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminDisruptionTimeRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.FlightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        if (string.IsNullOrWhiteSpace(request.DepartureDate) ||
            !DateOnly.TryParseExact(request.DepartureDate, "yyyy-MM-dd", out _))
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");

        if (string.IsNullOrWhiteSpace(request.NewDepartureTime))
            return await req.BadRequestAsync("'newDepartureTime' is required.");

        if (string.IsNullOrWhiteSpace(request.NewArrivalTime))
            return await req.BadRequestAsync("'newArrivalTime' is required.");

        try
        {
            var command = new AdminDisruptionTimeCommand(
                request.FlightNumber,
                request.DepartureDate,
                request.NewDepartureTime,
                request.NewArrivalTime,
                request.Reason);

            var result = await _timeHandler.HandleAsync(command, cancellationToken);

            return await req.OkJsonAsync(result);
        }
        catch (NotImplementedException)
        {
            return req.CreateResponse(HttpStatusCode.NotImplemented);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Time change disruption failed for flight {FlightNumber} on {DepartureDate}",
                request.FlightNumber, request.DepartureDate);
            return await req.InternalServerErrorAsync();
        }
    }
}
