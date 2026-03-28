using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.BatchCreateFlights;
using ReservationSystem.Microservices.Offer.Application.CreateFlight;
using ReservationSystem.Microservices.Offer.Application.CreateFare;
using ReservationSystem.Microservices.Offer.Application.SearchOffers;
using ReservationSystem.Microservices.Offer.Application.GetStoredOffer;
using ReservationSystem.Microservices.Offer.Application.HoldInventory;
using ReservationSystem.Microservices.Offer.Application.SellInventory;
using ReservationSystem.Microservices.Offer.Application.ReleaseInventory;
using ReservationSystem.Microservices.Offer.Application.CancelInventory;
using ReservationSystem.Microservices.Offer.Application.GetSeatAvailability;
using ReservationSystem.Microservices.Offer.Application.ReserveSeat;
using ReservationSystem.Microservices.Offer.Application.UpdateSeatStatus;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Offer.Models.Requests;
using ReservationSystem.Microservices.Offer.Models.Responses;

namespace ReservationSystem.Microservices.Offer.Functions;

public sealed class OfferFunction
{
    private readonly CreateFlightHandler _createFlightHandler;
    private readonly CreateFareHandler _createFareHandler;
    private readonly BatchCreateFlightsHandler _batchCreateFlightsHandler;
    private readonly SearchOffersHandler _searchHandler;
    private readonly GetStoredOfferHandler _getOfferHandler;
    private readonly HoldInventoryHandler _holdHandler;
    private readonly SellInventoryHandler _sellHandler;
    private readonly ReleaseInventoryHandler _releaseHandler;
    private readonly CancelInventoryHandler _cancelHandler;
    private readonly GetSeatAvailabilityHandler _seatAvailabilityHandler;
    private readonly ReserveSeatHandler _reserveSeatHandler;
    private readonly UpdateSeatStatusHandler _updateSeatStatusHandler;
    private readonly ILogger<OfferFunction> _logger;

    public OfferFunction(
        CreateFlightHandler createFlightHandler,
        CreateFareHandler createFareHandler,
        BatchCreateFlightsHandler batchCreateFlightsHandler,
        SearchOffersHandler searchHandler,
        GetStoredOfferHandler getOfferHandler,
        HoldInventoryHandler holdHandler,
        SellInventoryHandler sellHandler,
        ReleaseInventoryHandler releaseHandler,
        CancelInventoryHandler cancelHandler,
        GetSeatAvailabilityHandler seatAvailabilityHandler,
        ReserveSeatHandler reserveSeatHandler,
        UpdateSeatStatusHandler updateSeatStatusHandler,
        ILogger<OfferFunction> logger)
    {
        _createFlightHandler = createFlightHandler;
        _createFareHandler = createFareHandler;
        _batchCreateFlightsHandler = batchCreateFlightsHandler;
        _searchHandler = searchHandler;
        _getOfferHandler = getOfferHandler;
        _holdHandler = holdHandler;
        _sellHandler = sellHandler;
        _releaseHandler = releaseHandler;
        _cancelHandler = cancelHandler;
        _seatAvailabilityHandler = seatAvailabilityHandler;
        _reserveSeatHandler = reserveSeatHandler;
        _updateSeatStatusHandler = updateSeatStatusHandler;
        _logger = logger;
    }

    // POST /v1/flights
    [Function("CreateFlight")]
    [OpenApiOperation(operationId: "CreateFlight", tags: new[] { "Flights" }, Summary = "Create a new flight inventory record")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateFlightRequest), Required = true, Description = "Flight details including flightNumber, departureDate, origin, destination, aircraftType, cabinCode, totalSeats")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(FlightInventoryResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – flight already exists")]
    public async Task<HttpResponseData> CreateFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flights")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var command = new CreateFlightCommand(
            FlightNumber: body.GetProperty("flightNumber").GetString()!,
            DepartureDate: body.GetProperty("departureDate").GetString()!,
            DepartureTime: body.GetProperty("departureTime").GetString()!,
            ArrivalTime: body.GetProperty("arrivalTime").GetString()!,
            ArrivalDayOffset: body.TryGetProperty("arrivalDayOffset", out var ado) ? ado.GetInt32() : 0,
            Origin: body.GetProperty("origin").GetString()!,
            Destination: body.GetProperty("destination").GetString()!,
            AircraftType: body.GetProperty("aircraftType").GetString()!,
            CabinCode: body.GetProperty("cabinCode").GetString()!,
            TotalSeats: body.GetProperty("totalSeats").GetInt32());

        try
        {
            var inventory = await _createFlightHandler.HandleAsync(command, ct);
            return await req.CreatedAsync($"/v1/flights/{inventory.InventoryId}", new
            {
                inventoryId = inventory.InventoryId,
                flightNumber = inventory.FlightNumber,
                departureDate = inventory.DepartureDate.ToString("yyyy-MM-dd"),
                cabinCode = inventory.CabinCode,
                totalSeats = inventory.TotalSeats,
                seatsAvailable = inventory.SeatsAvailable,
                seatsHeld = inventory.SeatsHeld,
                seatsSold = inventory.SeatsSold,
                status = inventory.Status
            });
        }
        catch (InvalidOperationException ex)
        {
            return await req.ConflictAsync(ex.Message);
        }
    }

    // POST /v1/flights/{inventoryId}/fares
    [Function("CreateFare")]
    [OpenApiOperation(operationId: "CreateFare", tags: new[] { "Flights" }, Summary = "Add a fare to a flight inventory")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateFareRequest), Required = true, Description = "Fare details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(FareResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> CreateFare(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flights/{inventoryId:guid}/fares")] HttpRequestData req,
        Guid inventoryId, CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var command = new CreateFareCommand(
            InventoryId: inventoryId,
            FareBasisCode: body.GetProperty("fareBasisCode").GetString()!,
            FareFamily: body.TryGetProperty("fareFamily", out var ff) && ff.ValueKind != JsonValueKind.Null ? ff.GetString() : null,
            BookingClass: body.TryGetProperty("bookingClass", out var bc) && bc.ValueKind != JsonValueKind.Null ? bc.GetString() : null,
            CurrencyCode: body.GetProperty("currencyCode").GetString()!,
            BaseFareAmount: body.GetProperty("baseFareAmount").GetDecimal(),
            TaxAmount: body.GetProperty("taxAmount").GetDecimal(),
            IsRefundable: body.GetProperty("isRefundable").GetBoolean(),
            IsChangeable: body.GetProperty("isChangeable").GetBoolean(),
            ChangeFeeAmount: body.GetProperty("changeFeeAmount").GetDecimal(),
            CancellationFeeAmount: body.GetProperty("cancellationFeeAmount").GetDecimal(),
            PointsPrice: body.TryGetProperty("pointsPrice", out var pp) && pp.ValueKind != JsonValueKind.Null ? pp.GetInt32() : null,
            PointsTaxes: body.TryGetProperty("pointsTaxes", out var pt) && pt.ValueKind != JsonValueKind.Null ? pt.GetDecimal() : null,
            ValidFrom: body.GetProperty("validFrom").GetString()!,
            ValidTo: body.GetProperty("validTo").GetString()!);

        try
        {
            var fare = await _createFareHandler.HandleAsync(command, ct);
            return await req.CreatedAsync($"/v1/flights/{inventoryId}/fares/{fare.FareId}", new
            {
                fareId = fare.FareId,
                inventoryId = fare.InventoryId,
                fareBasisCode = fare.FareBasisCode,
                totalAmount = fare.TotalAmount
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.ConflictAsync(ex.Message); }
    }

    // POST /v1/flights/batch
    [Function("BatchCreateFlights")]
    [OpenApiOperation(operationId: "BatchCreateFlights", tags: new[] { "Flights" }, Summary = "Batch-create flight inventory records, skipping any that already exist")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(BatchCreateFlightsRequest), Required = true, Description = "Array of flight inventory definitions to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BatchCreateFlightsResponse), Description = "OK — returns counts of created and skipped records with created inventory details")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> BatchCreateFlights(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flights/batch")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("flights", out var flightsEl) || flightsEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'flights' array is required.");

        var items = new List<BatchFlightItem>();
        foreach (var f in flightsEl.EnumerateArray())
        {
            items.Add(new BatchFlightItem(
                FlightNumber: f.GetProperty("flightNumber").GetString()!,
                DepartureDate: f.GetProperty("departureDate").GetString()!,
                DepartureTime: f.GetProperty("departureTime").GetString()!,
                ArrivalTime: f.GetProperty("arrivalTime").GetString()!,
                ArrivalDayOffset: f.TryGetProperty("arrivalDayOffset", out var ado) ? ado.GetInt32() : 0,
                Origin: f.GetProperty("origin").GetString()!,
                Destination: f.GetProperty("destination").GetString()!,
                AircraftType: f.GetProperty("aircraftType").GetString()!,
                CabinCode: f.GetProperty("cabinCode").GetString()!,
                TotalSeats: f.GetProperty("totalSeats").GetInt32()));
        }

        if (items.Count == 0)
            return await req.BadRequestAsync("'flights' array must contain at least one item.");

        var result = await _batchCreateFlightsHandler.HandleAsync(
            new BatchCreateFlightsCommand(items.AsReadOnly()), ct);

        return await req.OkJsonAsync(new
        {
            created = result.Created.Count,
            skipped = result.SkippedCount,
            inventories = result.Created.Select(inv => new
            {
                inventoryId = inv.InventoryId,
                flightNumber = inv.FlightNumber,
                departureDate = inv.DepartureDate.ToString("yyyy-MM-dd"),
                cabinCode = inv.CabinCode,
                totalSeats = inv.TotalSeats,
                seatsAvailable = inv.SeatsAvailable,
                status = inv.Status
            })
        });
    }

    // POST /v1/search
    [Function("SearchOffers")]
    [OpenApiOperation(operationId: "SearchOffers", tags: new[] { "Offers" }, Summary = "Search for available flight offers")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchOffersRequest), Required = true, Description = "Search criteria: origin, destination, departureDate, cabinCode, paxCount")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SearchOffersResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/search")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var command = new SearchOffersCommand(
            Origin: body.GetProperty("origin").GetString()!,
            Destination: body.GetProperty("destination").GetString()!,
            DepartureDate: body.GetProperty("departureDate").GetString()!,
            CabinCode: body.GetProperty("cabinCode").GetString()!,
            PaxCount: body.GetProperty("paxCount").GetInt32(),
            BookingType: body.TryGetProperty("bookingType", out var bt) ? bt.GetString()! : "Revenue");

        var offers = await _searchHandler.HandleAsync(command, ct);

        var responseOffers = offers.Select(o => new
        {
            offerId = o.OfferId,
            inventoryId = o.InventoryId,
            flightNumber = o.FlightNumber,
            departureDate = o.DepartureDate.ToString("yyyy-MM-dd"),
            departureTime = o.DepartureTime.ToString("HH:mm"),
            arrivalTime = o.ArrivalTime.ToString("HH:mm"),
            arrivalDayOffset = o.ArrivalDayOffset,
            origin = o.Origin,
            destination = o.Destination,
            aircraftType = o.AircraftType,
            cabinCode = o.CabinCode,
            fareBasisCode = o.FareBasisCode,
            fareFamily = o.FareFamily,
            bookingClass = o.BookingClass,
            currencyCode = o.CurrencyCode,
            baseFareAmount = o.BaseFareAmount,
            taxAmount = o.TaxAmount,
            totalAmount = o.TotalAmount,
            isRefundable = o.IsRefundable,
            isChangeable = o.IsChangeable,
            changeFeeAmount = o.ChangeFeeAmount,
            cancellationFeeAmount = o.CancellationFeeAmount,
            pointsPrice = o.PointsPrice,
            pointsTaxes = o.PointsTaxes,
            bookingType = o.BookingType,
            seatsAvailable = o.SeatsAvailable,
            operatingCarrier = (string?)null,
            operatingFlightNumber = (string?)null,
            expiresAt = o.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        }).ToList();

        return await req.OkJsonAsync(new
        {
            origin = command.Origin,
            destination = command.Destination,
            departureDate = command.DepartureDate,
            offers = responseOffers
        });
    }

    // GET /v1/offers/{offerId}
    [Function("GetStoredOffer")]
    [OpenApiOperation(operationId: "GetStoredOffer", tags: new[] { "Offers" }, Summary = "Retrieve a stored offer by ID")]
    [OpenApiParameter(name: "offerId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Offer ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(StoredOfferResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Gone, Description = "Gone – offer has expired")]
    public async Task<HttpResponseData> GetOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/offers/{offerId:guid}")] HttpRequestData req,
        Guid offerId, CancellationToken ct)
    {
        StoredOffer? offer;
        try
        {
            offer = await _getOfferHandler.HandleAsync(new GetStoredOfferQuery(offerId), ct);
        }
        catch (OfferGoneException ex)
        {
            return await req.GoneAsync(ex.Message);
        }

        if (offer is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(new
        {
            offerId = offer.OfferId,
            inventoryId = offer.InventoryId,
            flightNumber = offer.FlightNumber,
            departureDate = offer.DepartureDate.ToString("yyyy-MM-dd"),
            departureTime = offer.DepartureTime.ToString("HH:mm"),
            arrivalTime = offer.ArrivalTime.ToString("HH:mm"),
            arrivalDayOffset = offer.ArrivalDayOffset,
            origin = offer.Origin,
            destination = offer.Destination,
            aircraftType = offer.AircraftType,
            cabinCode = offer.CabinCode,
            fareBasisCode = offer.FareBasisCode,
            fareFamily = offer.FareFamily,
            bookingClass = offer.BookingClass,
            currencyCode = offer.CurrencyCode,
            baseFareAmount = offer.BaseFareAmount,
            taxAmount = offer.TaxAmount,
            totalAmount = offer.TotalAmount,
            isRefundable = offer.IsRefundable,
            isChangeable = offer.IsChangeable,
            changeFeeAmount = offer.ChangeFeeAmount,
            cancellationFeeAmount = offer.CancellationFeeAmount,
            pointsPrice = offer.PointsPrice,
            pointsTaxes = offer.PointsTaxes,
            bookingType = offer.BookingType,
            seatsAvailable = offer.SeatsAvailable,
            operatingCarrier = (string?)null,
            operatingFlightNumber = (string?)null,
            isConsumed = offer.IsConsumed,
            expiresAt = offer.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }

    // POST /v1/inventory/hold
    [Function("HoldInventory")]
    [OpenApiOperation(operationId: "HoldInventory", tags: new[] { "Inventory" }, Summary = "Hold seats in inventory for a basket")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(HoldInventoryRequest), Required = true, Description = "Hold request: inventoryId, cabinCode, paxCount, basketId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(InventoryStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable Entity – insufficient seats")]
    public async Task<HttpResponseData> Hold(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/inventory/hold")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var command = new HoldInventoryCommand(
            InventoryId: body.GetProperty("inventoryId").GetGuid(),
            CabinCode: body.GetProperty("cabinCode").GetString()!,
            PaxCount: body.GetProperty("paxCount").GetInt32(),
            BasketId: body.GetProperty("basketId").GetGuid());

        try
        {
            var inventory = await _holdHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                inventoryId = inventory.InventoryId,
                seatsHeld = inventory.SeatsHeld,
                seatsAvailable = inventory.SeatsAvailable,
                seatsSold = inventory.SeatsSold
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // POST /v1/inventory/sell
    [Function("SellInventory")]
    [OpenApiOperation(operationId: "SellInventory", tags: new[] { "Inventory" }, Summary = "Sell (confirm) held inventory seats")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SellInventoryRequest), Required = true, Description = "Sell request: inventoryIds, paxCount, basketId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SellInventoryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable Entity")]
    public async Task<HttpResponseData> Sell(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/inventory/sell")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var inventoryIds = body.GetProperty("inventoryIds").EnumerateArray()
            .Select(e => e.GetGuid()).ToList();

        var command = new SellInventoryCommand(
            InventoryIds: inventoryIds,
            PaxCount: body.GetProperty("paxCount").GetInt32(),
            BasketId: body.GetProperty("basketId").GetGuid());

        try
        {
            var inventories = await _sellHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                sold = inventories.Select(i => new
                {
                    inventoryId = i.InventoryId,
                    seatsSold = i.SeatsSold,
                    seatsHeld = i.SeatsHeld,
                    seatsAvailable = i.SeatsAvailable
                })
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // POST /v1/inventory/release
    [Function("ReleaseInventory")]
    [OpenApiOperation(operationId: "ReleaseInventory", tags: new[] { "Inventory" }, Summary = "Release held inventory seats")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ReleaseInventoryRequest), Required = true, Description = "Release request: inventoryId, paxCount, releaseType, basketId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(InventoryStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Release(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/inventory/release")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var command = new ReleaseInventoryCommand(
            InventoryId: body.GetProperty("inventoryId").GetGuid(),
            PaxCount: body.GetProperty("paxCount").GetInt32(),
            ReleaseType: body.GetProperty("releaseType").GetString()!,
            BasketId: body.TryGetProperty("basketId", out var bid) && bid.ValueKind != JsonValueKind.Null ? bid.GetGuid() : null);

        try
        {
            var inventory = await _releaseHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                inventoryId = inventory.InventoryId,
                seatsAvailable = inventory.SeatsAvailable,
                seatsHeld = inventory.SeatsHeld,
                seatsSold = inventory.SeatsSold
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/inventory/cancel
    [Function("CancelInventory")]
    [OpenApiOperation(operationId: "CancelInventory", tags: new[] { "Inventory" }, Summary = "Cancel all inventory for a flight")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CancelInventoryRequest), Required = true, Description = "Cancellation request: flightNumber, departureDate")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CancelInventoryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Cancel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/inventory/cancel")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var command = new CancelInventoryCommand(
            FlightNumber: body.GetProperty("flightNumber").GetString()!,
            DepartureDate: body.GetProperty("departureDate").GetString()!);

        try
        {
            var count = await _cancelHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                flightNumber = command.FlightNumber,
                departureDate = command.DepartureDate,
                inventoriesCancelled = count,
                status = "Cancelled"
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // GET /v1/flights/{flightId}/seat-availability
    [Function("GetSeatAvailability")]
    [OpenApiOperation(operationId: "GetSeatAvailability", tags: new[] { "Seats" }, Summary = "Get seat availability for a flight")]
    [OpenApiParameter(name: "flightId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatAvailabilityResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetSeatAvailability(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{flightId:guid}/seat-availability")] HttpRequestData req,
        Guid flightId, CancellationToken ct)
    {
        var result = await _seatAvailabilityHandler.HandleAsync(new GetSeatAvailabilityQuery(flightId), ct);

        if (result is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var (inventory, seats) = result.Value;
        return await req.OkJsonAsync(new
        {
            flightId = inventory.InventoryId,
            flightNumber = inventory.FlightNumber,
            departureDate = inventory.DepartureDate.ToString("yyyy-MM-dd"),
            cabinCode = inventory.CabinCode,
            seatAvailability = seats.Select(s => new
            {
                seatOfferId = s.SeatOfferId,
                seatNumber = s.SeatNumber,
                status = s.Status
            })
        });
    }

    // POST /v1/flights/{flightId}/seat-reservations
    [Function("ReserveSeat")]
    [OpenApiOperation(operationId: "ReserveSeat", tags: new[] { "Seats" }, Summary = "Reserve specific seats on a flight")]
    [OpenApiParameter(name: "flightId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ReserveSeatRequest), Required = true, Description = "Seat reservation request: seatNumbers, basketId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ReserveSeatResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – seat already reserved")]
    public async Task<HttpResponseData> ReserveSeat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flights/{flightId:guid}/seat-reservations")] HttpRequestData req,
        Guid flightId, CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var seatNumbers = body.GetProperty("seatNumbers").EnumerateArray()
            .Select(e => e.GetString()!).ToList();

        var command = new ReserveSeatCommand(
            FlightId: flightId,
            BasketId: body.GetProperty("basketId").GetGuid(),
            SeatNumbers: seatNumbers);

        try
        {
            var reserved = await _reserveSeatHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new { flightId, reserved });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.ConflictAsync(ex.Message); }
    }

    // PATCH /v1/flights/{flightId}/seat-availability
    [Function("UpdateSeatStatus")]
    [OpenApiOperation(operationId: "UpdateSeatStatus", tags: new[] { "Seats" }, Summary = "Update seat status for a flight")]
    [OpenApiParameter(name: "flightId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateSeatStatusRequest), Required = true, Description = "Seat status update request: updates array of seatNumber/status pairs")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateSeatStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSeatStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flights/{flightId:guid}/seat-availability")] HttpRequestData req,
        Guid flightId, CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var updates = body.GetProperty("updates").EnumerateArray()
            .Select(u => new SeatStatusUpdate(
                u.GetProperty("seatNumber").GetString()!,
                u.GetProperty("status").GetString()!))
            .ToList();

        var command = new UpdateSeatStatusCommand(FlightId: flightId, Updates: updates);

        try
        {
            var count = await _updateSeatStatusHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new { updated = count });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
    }
}
