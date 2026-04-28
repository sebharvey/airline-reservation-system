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
using ReservationSystem.Microservices.Offer.Application.RebookInventory;
using ReservationSystem.Microservices.Offer.Application.CancelInventory;
using ReservationSystem.Microservices.Offer.Application.GetSeatAvailability;
using ReservationSystem.Microservices.Offer.Application.ReserveSeat;
using ReservationSystem.Microservices.Offer.Application.UpdateSeatStatus;
using ReservationSystem.Microservices.Offer.Application.GetFlightInventory;
using ReservationSystem.Microservices.Offer.Application.GetFlightAvailability;
using ReservationSystem.Microservices.Offer.Application.GetFlightInventoryByDate;
using ReservationSystem.Microservices.Offer.Application.GetFlightByInventoryId;
using ReservationSystem.Microservices.Offer.Application.GetInventoryHolds;
using ReservationSystem.Microservices.Offer.Application.RepriceStoredOffer;
using ReservationSystem.Microservices.Offer.Application.UpdateHoldSeat;
using ReservationSystem.Microservices.Offer.Application.UpdateInventoryAircraftType;
using ReservationSystem.Microservices.Offer.Application.SetInventoryOperationalData;
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
    private readonly RebookInventoryHandler _rebookInventoryHandler;
    private readonly CancelInventoryHandler _cancelHandler;
    private readonly GetSeatAvailabilityHandler _seatAvailabilityHandler;
    private readonly ReserveSeatHandler _reserveSeatHandler;
    private readonly UpdateSeatStatusHandler _updateSeatStatusHandler;
    private readonly GetFlightInventoryHandler _getFlightInventoryByFlightHandler;
    private readonly GetFlightAvailabilityHandler _getFlightAvailabilityHandler;
    private readonly GetFlightInventoryByDateHandler _getFlightInventoryHandler;
    private readonly GetFlightByInventoryIdHandler _getFlightByInventoryIdHandler;
    private readonly GetInventoryHoldsHandler _getInventoryHoldsHandler;
    private readonly RepriceStoredOfferHandler _repriceHandler;
    private readonly UpdateHoldSeatHandler _updateHoldSeatHandler;
    private readonly UpdateInventoryAircraftTypeHandler _updateAircraftTypeHandler;
    private readonly SetInventoryOperationalDataHandler _setOperationalDataHandler;
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
        RebookInventoryHandler rebookInventoryHandler,
        CancelInventoryHandler cancelHandler,
        GetSeatAvailabilityHandler seatAvailabilityHandler,
        ReserveSeatHandler reserveSeatHandler,
        UpdateSeatStatusHandler updateSeatStatusHandler,
        GetFlightInventoryHandler getFlightInventoryByFlightHandler,
        GetFlightAvailabilityHandler getFlightAvailabilityHandler,
        GetFlightInventoryByDateHandler getFlightInventoryHandler,
        GetFlightByInventoryIdHandler getFlightByInventoryIdHandler,
        GetInventoryHoldsHandler getInventoryHoldsHandler,
        RepriceStoredOfferHandler repriceHandler,
        UpdateHoldSeatHandler updateHoldSeatHandler,
        UpdateInventoryAircraftTypeHandler updateAircraftTypeHandler,
        SetInventoryOperationalDataHandler setOperationalDataHandler,
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
        _rebookInventoryHandler = rebookInventoryHandler;
        _cancelHandler = cancelHandler;
        _seatAvailabilityHandler = seatAvailabilityHandler;
        _reserveSeatHandler = reserveSeatHandler;
        _updateSeatStatusHandler = updateSeatStatusHandler;
        _getFlightInventoryByFlightHandler = getFlightInventoryByFlightHandler;
        _getFlightAvailabilityHandler = getFlightAvailabilityHandler;
        _getFlightInventoryHandler = getFlightInventoryHandler;
        _getFlightByInventoryIdHandler = getFlightByInventoryIdHandler;
        _getInventoryHoldsHandler = getInventoryHoldsHandler;
        _repriceHandler = repriceHandler;
        _updateHoldSeatHandler = updateHoldSeatHandler;
        _updateAircraftTypeHandler = updateAircraftTypeHandler;
        _setOperationalDataHandler = setOperationalDataHandler;
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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("cabins", out var cabinsEl) || cabinsEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'cabins' array is required.");

        var cabins = cabinsEl.EnumerateArray()
            .Select(c => new CabinItem(
                c.GetProperty("cabinCode").GetString()!,
                c.GetProperty("totalSeats").GetInt32()))
            .ToList().AsReadOnly();

        var command = new CreateFlightCommand(
            FlightNumber: body.GetProperty("flightNumber").GetString()!,
            DepartureDate: body.GetProperty("departureDate").GetString()!,
            DepartureTime: body.GetProperty("departureTime").GetString()!,
            ArrivalTime: body.GetProperty("arrivalTime").GetString()!,
            ArrivalDayOffset: body.TryGetProperty("arrivalDayOffset", out var ado) ? ado.GetInt32() : 0,
            Origin: body.GetProperty("origin").GetString()!,
            Destination: body.GetProperty("destination").GetString()!,
            AircraftType: body.GetProperty("aircraftType").GetString()!,
            Cabins: cabins);

        try
        {
            var inventory = await _createFlightHandler.HandleAsync(command, ct);
            return await req.CreatedAsync($"/v1/flights/{inventory.InventoryId}", new
            {
                inventoryId = inventory.InventoryId,
                flightNumber = inventory.FlightNumber,
                departureDate = inventory.DepartureDate.ToString("yyyy-MM-dd"),
                totalSeats = inventory.TotalSeats,
                seatsAvailable = inventory.SeatsAvailable,
                status = inventory.Status,
                cabins = inventory.Cabins.Select(c => new { cabinCode = c.CabinCode, totalSeats = c.TotalSeats, seatsAvailable = c.SeatsAvailable, seatsSold = c.SeatsSold, seatsHeld = c.SeatsHeld })
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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new CreateFareCommand(
            InventoryId: inventoryId,
            FareBasisCode: body.GetProperty("fareBasisCode").GetString()!,
            FareFamily: body.TryGetProperty("fareFamily", out var ff) && ff.ValueKind != JsonValueKind.Null ? ff.GetString() : null,
            CabinCode: body.GetProperty("cabinCode").GetString()!,
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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("flights", out var flightsEl) || flightsEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'flights' array is required.");

        var items = new List<BatchFlightItem>();
        foreach (var f in flightsEl.EnumerateArray())
        {
            if (!f.TryGetProperty("cabins", out var fCabinsEl) || fCabinsEl.ValueKind != JsonValueKind.Array)
                return await req.BadRequestAsync("Each flight must include a 'cabins' array.");

            var flightCabins = fCabinsEl.EnumerateArray()
                .Select(c => new CabinItem(
                    c.GetProperty("cabinCode").GetString()!,
                    c.GetProperty("totalSeats").GetInt32()))
                .ToList().AsReadOnly();

            items.Add(new BatchFlightItem(
                FlightNumber: f.GetProperty("flightNumber").GetString()!,
                DepartureDate: f.GetProperty("departureDate").GetString()!,
                DepartureTime: f.GetProperty("departureTime").GetString()!,
                ArrivalTime: f.GetProperty("arrivalTime").GetString()!,
                ArrivalDayOffset: f.TryGetProperty("arrivalDayOffset", out var ado) ? ado.GetInt32() : 0,
                Origin: f.GetProperty("origin").GetString()!,
                Destination: f.GetProperty("destination").GetString()!,
                AircraftType: f.GetProperty("aircraftType").GetString()!,
                Cabins: flightCabins,
                DepartureTimeUtc: f.TryGetProperty("departureTimeUtc", out var dtu) && dtu.ValueKind != JsonValueKind.Null ? dtu.GetString() : null,
                ArrivalTimeUtc: f.TryGetProperty("arrivalTimeUtc", out var atu) && atu.ValueKind != JsonValueKind.Null ? atu.GetString() : null,
                ArrivalDayOffsetUtc: f.TryGetProperty("arrivalDayOffsetUtc", out var adou) && adou.ValueKind != JsonValueKind.Null ? adou.GetInt32() : null));
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
                totalSeats = inv.TotalSeats,
                seatsAvailable = inv.SeatsAvailable,
                status = inv.Status,
                cabins = inv.Cabins.Select(c => new { cabinCode = c.CabinCode, totalSeats = c.TotalSeats, seatsAvailable = c.SeatsAvailable })
            })
        });
    }

    // POST /v1/search
    [Function("SearchOffers")]
    [OpenApiOperation(operationId: "SearchOffers", tags: new[] { "Offers" }, Summary = "Search for available flight offers")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchOffersRequest), Required = true, Description = "Search criteria: origin, destination, departureDate, paxCount")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SearchOffersResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/search")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new SearchOffersCommand(
            Origin: body.GetProperty("origin").GetString()!,
            Destination: body.GetProperty("destination").GetString()!,
            DepartureDate: body.GetProperty("departureDate").GetString()!,
            PaxCount: body.GetProperty("paxCount").GetInt32(),
            BookingType: body.TryGetProperty("bookingType", out var bt) ? bt.GetString()! : "Revenue",
            IncludePrivateFares: body.TryGetProperty("includePrivateFares", out var ipf) && ipf.ValueKind == JsonValueKind.True);

        var result = await _searchHandler.HandleAsync(command, ct);

        var sessionId = result?.Offer.SessionId ?? Guid.NewGuid();

        // Flight details come from the in-memory FlightInventory; fare items from FaresInfo.
        var flights = result is null ? [] : result.Inventories.Select(inv =>
        {
            var fi    = result.Offer.GetFaresInfo();
            var entry = fi.Inventories.FirstOrDefault(e => e.InventoryId == inv.InventoryId);
            return new
            {
                inventoryId      = inv.InventoryId,
                flightNumber     = inv.FlightNumber,
                origin           = inv.Origin,
                destination      = inv.Destination,
                departureDate    = inv.DepartureDate.ToString("yyyy-MM-dd"),
                departureTime    = inv.DepartureTime.ToString("HH:mm"),
                arrivalTime      = inv.ArrivalTime.ToString("HH:mm"),
                arrivalDayOffset = inv.ArrivalDayOffset,
                durationMinutes  = CalculateDurationMinutes(inv),
                aircraftType     = inv.AircraftType,
                expiresAt        = result.Offer.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                offers = (entry?.Offers ?? []).Select(item => new
                {
                    offerId               = item.OfferId,
                    cabinCode             = item.CabinCode,
                    fareBasisCode         = item.FareBasisCode,
                    fareFamily            = item.FareFamily,
                    currencyCode          = item.CurrencyCode,
                    baseFareAmount        = item.BaseFareAmount,
                    taxAmount             = item.TaxAmount,
                    totalAmount           = item.TotalAmount,
                    isRefundable          = item.IsRefundable,
                    isChangeable          = item.IsChangeable,
                    changeFeeAmount       = item.ChangeFeeAmount,
                    cancellationFeeAmount = item.CancellationFeeAmount,
                    pointsPrice           = item.PointsPrice,
                    pointsTaxes           = item.PointsTaxes,
                    seatsAvailable        = item.SeatsAvailable,
                    bookingType           = item.BookingType
                })
            };
        }).ToList();

        return await req.OkJsonAsync(new
        {
            sessionId,
            origin        = command.Origin,
            destination   = command.Destination,
            departureDate = command.DepartureDate,
            flights
        });
    }

    // GET /v1/offers/{offerId}
    [Function("GetStoredOffer")]
    [OpenApiOperation(operationId: "GetStoredOffer", tags: new[] { "Offers" }, Summary = "Retrieve a stored offer by ID")]
    [OpenApiParameter(name: "offerId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Offer ID")]
    [OpenApiParameter(name: "sessionId", In = ParameterLocation.Query, Required = false, Type = typeof(Guid), Description = "Session ID from search — scopes the lookup to this session for an efficient indexed query")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(StoredOfferResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Gone, Description = "Gone – offer has expired")]
    public async Task<HttpResponseData> GetOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/offers/{offerId:guid}")] HttpRequestData req,
        Guid offerId, CancellationToken ct)
    {
        var sessionIdRaw = req.Query["sessionId"];
        Guid? sessionId = Guid.TryParse(sessionIdRaw, out var sid) ? sid : null;

        GetStoredOfferResult? result;
        try
        {
            result = await _getOfferHandler.HandleAsync(new GetStoredOfferQuery(offerId, sessionId), ct);
        }
        catch (OfferGoneException ex)
        {
            return await req.GoneAsync(ex.Message);
        }

        if (result is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var offer = result.Offer;
        var inv   = result.Inventory;
        var entry = offer.GetFaresInfo().Inventories.FirstOrDefault(e => e.InventoryId == inv.InventoryId);

        return await req.OkJsonAsync(new
        {
            storedOfferId    = offer.StoredOfferId,
            sessionId        = offer.SessionId,
            expiresAt        = offer.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            inventoryId      = inv.InventoryId,
            validated        = entry?.Validated ?? false,
            flightNumber     = inv.FlightNumber,
            origin           = inv.Origin,
            destination      = inv.Destination,
            departureDate    = inv.DepartureDate.ToString("yyyy-MM-dd"),
            departureTime    = inv.DepartureTime.ToString("HH:mm"),
            arrivalTime      = inv.ArrivalTime.ToString("HH:mm"),
            arrivalDayOffset = inv.ArrivalDayOffset,
            aircraftType     = inv.AircraftType,
            offers           = (entry?.Offers ?? []).Select(item => new
            {
                offerId               = item.OfferId,
                cabinCode             = item.CabinCode,
                fareBasisCode         = item.FareBasisCode,
                fareFamily            = item.FareFamily,
                currencyCode          = item.CurrencyCode,
                baseFareAmount        = item.BaseFareAmount,
                taxAmount             = item.TaxAmount,
                totalAmount           = item.TotalAmount,
                isRefundable          = item.IsRefundable,
                isChangeable          = item.IsChangeable,
                changeFeeAmount       = item.ChangeFeeAmount,
                cancellationFeeAmount = item.CancellationFeeAmount,
                pointsPrice           = item.PointsPrice,
                pointsTaxes           = item.PointsTaxes,
                seatsAvailable        = item.SeatsAvailable,
                bookingType           = item.BookingType
            })
        });
    }

    // POST /v1/offers/{offerId}/reprice
    [Function("RepriceStoredOffer")]
    [OpenApiOperation(operationId: "RepriceStoredOffer", tags: new[] { "Offers" }, Summary = "Reprice a stored offer — fetch tax lines from the matching FareRule and set validated = true")]
    [OpenApiParameter(name: "offerId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Offer ID")]
    [OpenApiParameter(name: "sessionId", In = ParameterLocation.Query, Required = false, Type = typeof(Guid), Description = "Session ID — scopes the lookup to the indexed session for efficiency")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK — repriced inventory entry with tax lines and validated = true")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Gone, Description = "Gone — offer has expired")]
    public async Task<HttpResponseData> Reprice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/offers/{offerId:guid}/reprice")] HttpRequestData req,
        Guid offerId,
        CancellationToken ct)
    {
        var sessionIdRaw = req.Query["sessionId"];
        Guid? sessionId = Guid.TryParse(sessionIdRaw, out var sid) ? sid : null;

        RepriceStoredOfferResult? result;
        try
        {
            result = await _repriceHandler.HandleAsync(new RepriceStoredOfferCommand(offerId, sessionId), ct);
        }
        catch (OfferGoneException ex)
        {
            return await req.GoneAsync(ex.Message);
        }

        if (result is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(new
        {
            storedOfferId = result.StoredOfferId,
            sessionId     = result.SessionId,
            inventoryId   = result.InventoryId,
            validated     = result.Validated,
            offers        = result.Offers.Select(item => new
            {
                offerId               = item.OfferId,
                cabinCode             = item.CabinCode,
                fareBasisCode         = item.FareBasisCode,
                fareFamily            = item.FareFamily,
                currencyCode          = item.CurrencyCode,
                baseFareAmount        = item.BaseFareAmount,
                taxAmount             = item.TaxAmount,
                totalAmount           = item.TotalAmount,
                taxLines              = item.TaxLines,
                isRefundable          = item.IsRefundable,
                isChangeable          = item.IsChangeable,
                seatsAvailable        = item.SeatsAvailable,
                bookingType           = item.BookingType
            })
        });
    }

    // POST /v1/inventory/hold
    [Function("HoldInventory")]
    [OpenApiOperation(operationId: "HoldInventory", tags: new[] { "Inventory" }, Summary = "Hold seats in inventory for an order")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(HoldInventoryRequest), Required = true, Description = "Hold request: inventoryId, cabinCode, paxCount, orderId")]
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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        // Accept both the new per-pax passengers array and the legacy paxCount integer so that
        // the function keeps working while Orchestration.Retail redeploys with the new format.
        List<PaxHold> passengers;
        if (body.TryGetProperty("passengers", out var passengersEl) && passengersEl.ValueKind == JsonValueKind.Array)
        {
            passengers = passengersEl.EnumerateArray()
                .Select(p => new PaxHold(
                    SeatNumber:  p.TryGetProperty("seatNumber",  out var sn)  && sn.ValueKind  == JsonValueKind.String ? sn.GetString()  : null,
                    PassengerId: p.TryGetProperty("passengerId", out var pid) && pid.ValueKind == JsonValueKind.String ? pid.GetString() : null))
                .ToList();
        }
        else if (body.TryGetProperty("paxCount", out var paxCountEl) && paxCountEl.ValueKind == JsonValueKind.Number)
        {
            passengers = Enumerable.Repeat(new PaxHold(null, null), paxCountEl.GetInt32()).ToList();
        }
        else
        {
            return await req.BadRequestAsync("Either 'passengers' array or 'paxCount' is required.");
        }

        var holdType = body.TryGetProperty("holdType", out var htEl) && htEl.ValueKind == JsonValueKind.String
            ? htEl.GetString() ?? "Revenue"
            : "Revenue";
        short? standbyPriority = body.TryGetProperty("standbyPriority", out var spEl) && spEl.ValueKind == JsonValueKind.Number
            ? spEl.GetInt16()
            : null;

        HoldInventoryCommand command;
        try
        {
            command = new HoldInventoryCommand(
                InventoryId: body.GetProperty("inventoryId").GetGuid(),
                CabinCode: body.GetProperty("cabinCode").GetString()!,
                Passengers: passengers,
                OrderId: body.GetProperty("orderId").GetGuid(),
                HoldType: holdType,
                StandbyPriority: standbyPriority);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return await req.BadRequestAsync($"Invalid request body: {ex.Message}");
        }

        try
        {
            var inventory = await _holdHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                inventoryId = inventory.InventoryId,
                seatsAvailable = inventory.SeatsAvailable,
                cabins = inventory.Cabins.Select(c => new { cabinCode = c.CabinCode, seatsAvailable = c.SeatsAvailable, seatsSold = c.SeatsSold, seatsHeld = c.SeatsHeld })
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // POST /v1/inventory/sell
    [Function("SellInventory")]
    [OpenApiOperation(operationId: "SellInventory", tags: new[] { "Inventory" }, Summary = "Sell (confirm) held inventory seats")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SellInventoryRequest), Required = true, Description = "Sell request: inventoryIds, paxCount, orderId")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SellInventoryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable Entity")]
    public async Task<HttpResponseData> Sell(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/inventory/sell")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var sellItems = body.GetProperty("items").EnumerateArray()
            .Select(e => new SellInventoryItem(
                e.GetProperty("inventoryId").GetGuid(),
                e.GetProperty("cabinCode").GetString()!))
            .ToList();

        var command = new SellInventoryCommand(
            Items: sellItems,
            OrderId: body.GetProperty("orderId").GetGuid());

        try
        {
            var inventories = await _sellHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                sold = inventories.Select(i => new
                {
                    inventoryId = i.InventoryId,
                    seatsAvailable = i.SeatsAvailable,
                    cabins = i.Cabins.Select(c => new { cabinCode = c.CabinCode, seatsAvailable = c.SeatsAvailable, seatsSold = c.SeatsSold, seatsHeld = c.SeatsHeld })
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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new ReleaseInventoryCommand(
            InventoryId: body.GetProperty("inventoryId").GetGuid(),
            CabinCode: body.GetProperty("cabinCode").GetString()!,
            OrderId: body.GetProperty("orderId").GetGuid(),
            ReleaseType: body.GetProperty("releaseType").GetString()!,
            BasketId: body.TryGetProperty("basketId", out var bid) && bid.ValueKind != JsonValueKind.Null ? bid.GetGuid() : null);

        try
        {
            var inventory = await _releaseHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                inventoryId = inventory.InventoryId,
                seatsAvailable = inventory.SeatsAvailable,
                cabins = inventory.Cabins.Select(c => new { cabinCode = c.CabinCode, seatsAvailable = c.SeatsAvailable, seatsSold = c.SeatsSold, seatsHeld = c.SeatsHeld })
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (ArgumentException ex) { return await req.BadRequestAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // POST /v1/inventory/rebook
    [Function("RebookInventory")]
    [OpenApiOperation(operationId: "RebookInventory", tags: new[] { "Inventory" }, Summary = "Atomically sell replacement inventory and release original — used in IROPS rebook")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "{ fromInventoryId, fromCabinCode, toItems: [{inventoryId, cabinCode}], orderId }")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content — rebook complete")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable Entity")]
    public async Task<HttpResponseData> RebookInventory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/inventory/rebook")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var toItems = body.GetProperty("toItems").EnumerateArray()
            .Select(e => new SellInventoryItem(
                e.GetProperty("inventoryId").GetGuid(),
                e.GetProperty("cabinCode").GetString()!))
            .ToList();

        var command = new RebookInventoryCommand(
            FromInventoryId: body.GetProperty("fromInventoryId").GetGuid(),
            FromCabinCode:   body.GetProperty("fromCabinCode").GetString()!,
            ToItems:         toItems,
            OrderId:         body.GetProperty("orderId").GetGuid());

        try
        {
            await _rebookInventoryHandler.HandleAsync(command, ct);
            return req.CreateResponse(HttpStatusCode.NoContent);
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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

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
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

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

    // GET /v1/flights/{inventoryId}
    [Function("GetFlightByInventoryId")]
    [OpenApiOperation(operationId: "GetFlightByInventoryId", tags: new[] { "Flights" }, Summary = "Get flight details for a specific inventory record by ID")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID (GUID)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FlightInventoryGroupResponse), Description = "OK — returns flight details for the inventory record")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no inventory record exists for the given ID")]
    public async Task<HttpResponseData> GetFlightByInventoryId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{inventoryId:guid}")] HttpRequestData req,
        Guid inventoryId, CancellationToken ct)
    {
        var inventory = await _getFlightByInventoryIdHandler.HandleAsync(
            new GetFlightByInventoryIdQuery(inventoryId), ct);

        if (inventory is null)
            return await req.NotFoundAsync($"No inventory record found for ID '{inventoryId}'.");

        return await req.OkJsonAsync(new
        {
            inventoryId   = inventory.InventoryId,
            flightNumber  = inventory.FlightNumber,
            origin        = inventory.Origin,
            destination   = inventory.Destination,
            departureDate = inventory.DepartureDate.ToString("yyyy-MM-dd"),
            departureTime = inventory.DepartureTime.ToString("HH:mm"),
            arrivalTime   = inventory.ArrivalTime.ToString("HH:mm"),
            arrivalDayOffset = inventory.ArrivalDayOffset,
            aircraftType  = inventory.AircraftType,
            status        = inventory.Status
        });
    }

    // GET /v1/flights/{flightNumber}/inventory?departureDate=yyyy-MM-dd
    [Function("GetFlightInventory")]
    [OpenApiOperation(operationId: "GetFlightInventory", tags: new[] { "Flights" }, Summary = "Get flight inventory for a specific flight number and departure date")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Flight number (e.g. AX001)")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Departure date (yyyy-MM-dd). Defaults to today.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FlightInventoryGroupResponse), Description = "OK — returns flight inventory with cabin breakdown")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no inventory exists for the given flight and date")]
    public async Task<HttpResponseData> GetFlightInventory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{flightNumber}/inventory")] HttpRequestData req,
        string flightNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(flightNumber))
            return await req.BadRequestAsync("'flightNumber' is required.");

        var dateParam = System.Web.HttpUtility.ParseQueryString(req.Url.Query)["departureDate"];

        DateOnly departureDate;
        if (string.IsNullOrWhiteSpace(dateParam))
        {
            departureDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!DateOnly.TryParseExact(dateParam, "yyyy-MM-dd", out departureDate))
        {
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");
        }

        var result = await _getFlightInventoryByFlightHandler.HandleAsync(
            new GetFlightInventoryQuery(flightNumber.ToUpperInvariant(), departureDate), ct);

        if (result is null)
            return await req.NotFoundAsync($"No inventory found for flight '{flightNumber}' on {departureDate:yyyy-MM-dd}.");

        var first = result.First;
        var response = new FlightInventoryGroupResponse
        {
            FlightNumber        = first.FlightNumber,
            DepartureDate       = first.DepartureDate.ToString("yyyy-MM-dd"),
            DepartureTime       = first.DepartureTime.ToString("HH:mm"),
            ArrivalTime         = first.ArrivalTime.ToString("HH:mm"),
            ArrivalDayOffset    = first.ArrivalDayOffset,
            Origin              = first.Origin,
            Destination         = first.Destination,
            AircraftType        = first.AircraftType,
            Status              = first.Status,
            TotalSeats          = result.TotalSeats,
            TotalSeatsAvailable = result.TotalAvailable,
            LoadFactor          = result.TotalSeats > 0
                ? (int)Math.Round((double)(result.TotalSeats - result.TotalAvailable) / result.TotalSeats * 100)
                : 0,
            F = MapCabinAggregation(result.CabinAggregations, "F"),
            J = MapCabinAggregation(result.CabinAggregations, "J"),
            W = MapCabinAggregation(result.CabinAggregations, "W"),
            Y = MapCabinAggregation(result.CabinAggregations, "Y"),
        };

        return await req.OkJsonAsync(response);
    }

    private static Models.Responses.CabinInventory? MapCabinAggregation(
        IReadOnlyDictionary<string, CabinAggregation> aggregations, string cabinCode)
    {
        if (!aggregations.TryGetValue(cabinCode, out var agg))
            return null;

        return new Models.Responses.CabinInventory
        {
            TotalSeats = agg.TotalSeats,
            SeatsAvailable = agg.SeatsAvailable,
            SeatsSold = agg.SeatsSold,
            SeatsHeld = agg.SeatsHeld
        };
    }

    // GET /v1/flights/availability?origin={}&destination={}&fromDate={}&days={}
    [Function("GetFlightAvailability")]
    [OpenApiOperation(operationId: "GetFlightAvailability", tags: new[] { "Flights" }, Summary = "Get available flights on a route over a date range — cabin-level seat counts, no fare pricing")]
    [OpenApiParameter(name: "origin",      In = ParameterLocation.Query, Required = true,  Type = typeof(string), Description = "Origin airport IATA code (e.g. LHR)")]
    [OpenApiParameter(name: "destination", In = ParameterLocation.Query, Required = true,  Type = typeof(string), Description = "Destination airport IATA code (e.g. JFK)")]
    [OpenApiParameter(name: "fromDate",    In = ParameterLocation.Query, Required = true,  Type = typeof(string), Description = "Start date inclusive (yyyy-MM-dd)")]
    [OpenApiParameter(name: "days",        In = ParameterLocation.Query, Required = false, Type = typeof(int),    Description = "Number of days to cover (default 7, max 28)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK — list of available flights with per-cabin seat counts")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> GetFlightAvailability(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/availability")] HttpRequestData req,
        CancellationToken ct)
    {
        var qs          = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var origin      = qs["origin"];
        var destination = qs["destination"];
        var fromDateRaw = qs["fromDate"];
        var daysRaw     = qs["days"];

        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
            return await req.BadRequestAsync("'origin' and 'destination' are required.");

        if (!DateOnly.TryParseExact(fromDateRaw, "yyyy-MM-dd", out var fromDate))
            return await req.BadRequestAsync("'fromDate' must be in yyyy-MM-dd format.");

        var days   = int.TryParse(daysRaw, out var d) ? Math.Clamp(d, 1, 28) : 7;
        var toDate = fromDate.AddDays(days - 1);

        var flights = await _getFlightAvailabilityHandler.HandleAsync(
            new GetFlightAvailabilityQuery(origin.ToUpperInvariant(), destination.ToUpperInvariant(), fromDate, toDate), ct);

        return await req.OkJsonAsync(new
        {
            origin      = origin.ToUpperInvariant(),
            destination = destination.ToUpperInvariant(),
            fromDate    = fromDate.ToString("yyyy-MM-dd"),
            toDate      = toDate.ToString("yyyy-MM-dd"),
            flights     = flights.Select(f => new
            {
                inventoryId      = f.InventoryId,
                flightNumber     = f.FlightNumber,
                departureDate    = f.DepartureDate.ToString("yyyy-MM-dd"),
                departureTime    = f.DepartureTime.ToString("HH:mm"),
                departureTimeUtc = f.DepartureTimeUtc?.ToString("HH:mm"),
                arrivalTime      = f.ArrivalTime.ToString("HH:mm"),
                arrivalDayOffset = f.ArrivalDayOffset,
                origin           = f.Origin,
                destination      = f.Destination,
                cabins           = f.Cabins
                    .Where(c => c.SeatsAvailable > 0)
                    .Select(c => new { cabinCode = c.CabinCode, seatsAvailable = c.SeatsAvailable })
            })
        });
    }

    // GET /v1/admin/inventory?departureDate=yyyy-MM-dd
    [Function("GetFlightInventoryByDate")]
    [OpenApiOperation(operationId: "GetFlightInventoryByDate", tags: new[] { "Admin Inventory" }, Summary = "Get flight inventory grouped by cabin for a given departure date")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Departure date (yyyy-MM-dd). Defaults to today.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FlightInventoryGroupResponse>), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> GetFlightInventoryByDate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/inventory")] HttpRequestData req,
        CancellationToken ct)
    {
        var dateParam = System.Web.HttpUtility.ParseQueryString(req.Url.Query)["departureDate"];

        DateOnly departureDate;
        if (string.IsNullOrWhiteSpace(dateParam))
        {
            departureDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!DateOnly.TryParseExact(dateParam, "yyyy-MM-dd", out departureDate))
        {
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");
        }

        var results = await _getFlightInventoryHandler.HandleAsync(
            new GetFlightInventoryByDateQuery(departureDate), ct);

        var response = results.Select(r =>
        {
            var g = r.Group;
            return new FlightInventoryGroupResponse
            {
                InventoryId          = g.InventoryId,
                FlightNumber         = g.FlightNumber,
                DepartureDate        = g.DepartureDate.ToString("yyyy-MM-dd"),
                DepartureTime        = g.DepartureTime.ToString("HH:mm"),
                ArrivalTime          = g.ArrivalTime.ToString("HH:mm"),
                ArrivalDayOffset     = g.ArrivalDayOffset,
                Origin               = g.Origin,
                Destination          = g.Destination,
                AircraftType         = g.AircraftType,
                Status               = r.EffectiveStatus,
                DepartureGate        = g.DepartureGate,
                AircraftRegistration = g.AircraftRegistration,
                TotalSeats           = g.TotalSeats,
                TotalSeatsAvailable  = g.TotalSeatsAvailable,
                LoadFactor           = r.LoadFactor,
                F = MapCabinData(g.F),
                J = MapCabinData(g.J),
                W = MapCabinData(g.W),
                Y = MapCabinData(g.Y),
            };
        }).ToList();

        return await req.OkJsonAsync(response);
    }

    private static Models.Responses.CabinInventory? MapCabinData(Domain.Entities.FlightInventoryGroup.CabinData? data)
    {
        if (data is null) return null;
        return new Models.Responses.CabinInventory
        {
            TotalSeats = data.TotalSeats,
            SeatsAvailable = data.SeatsAvailable,
            SeatsSold = data.SeatsSold,
            SeatsHeld = data.SeatsHeld
        };
    }

    // GET /v1/inventory/{inventoryId}/holds
    [Function("GetInventoryHolds")]
    [OpenApiOperation(operationId: "GetInventoryHolds", tags: new[] { "Inventory" }, Summary = "Get all holds for a specific flight inventory record")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK — list of holds")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetInventoryHolds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/inventory/{inventoryId:guid}/holds")] HttpRequestData req,
        Guid inventoryId,
        CancellationToken ct)
    {
        var holds = await _getInventoryHoldsHandler.HandleAsync(new GetInventoryHoldsQuery(inventoryId), ct);

        return await req.OkJsonAsync(holds.Select(h => new
        {
            holdId          = h.HoldId,
            orderId         = h.OrderId,
            passengerId     = h.PassengerId,
            cabinCode       = h.CabinCode,
            seatNumber      = h.SeatNumber,
            status          = h.Status,
            holdType        = h.HoldType,
            standbyPriority = h.StandbyPriority,
            createdAt       = h.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        }));
    }

    // PATCH /v1/inventory/holds/seat
    [Function("UpdateHoldSeat")]
    [OpenApiOperation(operationId: "UpdateHoldSeat", tags: new[] { "Inventory" }, Summary = "Update the seat number on an existing inventory hold — called after OLCI auto-seat assignment")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "{ inventoryId, orderId, passengerId, seatNumber }")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content — seat updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — no matching hold")]
    public async Task<HttpResponseData> UpdateHoldSeat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/inventory/holds/seat")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("inventoryId", out var invEl) || !Guid.TryParse(invEl.GetString(), out var inventoryId))
            return await req.BadRequestAsync("'inventoryId' (GUID) is required.");
        if (!body.TryGetProperty("orderId", out var orderEl) || !Guid.TryParse(orderEl.GetString(), out var orderId))
            return await req.BadRequestAsync("'orderId' (GUID) is required.");
        if (!body.TryGetProperty("passengerId", out var paxEl) || string.IsNullOrWhiteSpace(paxEl.GetString()))
            return await req.BadRequestAsync("'passengerId' is required.");
        if (!body.TryGetProperty("seatNumber", out var seatEl) || string.IsNullOrWhiteSpace(seatEl.GetString()))
            return await req.BadRequestAsync("'seatNumber' is required.");

        var command = new UpdateHoldSeatCommand(inventoryId, orderId, paxEl.GetString()!, seatEl.GetString()!);
        var updated = await _updateHoldSeatHandler.HandleAsync(command, ct);

        return updated
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : await req.NotFoundAsync($"No hold found for passenger '{command.PassengerId}' on inventory '{inventoryId}' / order '{orderId}'.");
    }

    // PATCH /v1/inventory/aircraft-type
    [Function("UpdateInventoryAircraftType")]
    [OpenApiOperation(operationId: "UpdateInventoryAircraftType", tags: new[] { "Inventory" }, Summary = "Update the aircraft type on all inventory records for a flight")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "{ flightNumber, departureDate, newAircraftType }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateAircraftType(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/inventory/aircraft-type")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var command = new UpdateInventoryAircraftTypeCommand(
            FlightNumber: body.GetProperty("flightNumber").GetString()!,
            DepartureDate: body.GetProperty("departureDate").GetString()!,
            NewAircraftType: body.GetProperty("newAircraftType").GetString()!);

        try
        {
            var count = await _updateAircraftTypeHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(new
            {
                flightNumber = command.FlightNumber,
                departureDate = command.DepartureDate,
                newAircraftType = command.NewAircraftType,
                inventoriesUpdated = count
            });
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
    }

    // PATCH /v1/inventory/{inventoryId}/operational-data
    [Function("SetInventoryOperationalData")]
    [OpenApiOperation(operationId: "SetInventoryOperationalData", tags: new[] { "Inventory" }, Summary = "Set departure gate and/or aircraft registration for a flight inventory record")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "{ departureGate, aircraftRegistration }")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> SetOperationalData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/inventory/{inventoryId:guid}/operational-data")] HttpRequestData req,
        Guid inventoryId,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException ex) { _logger.LogWarning(ex, "Invalid JSON in request body"); return await req.BadRequestAsync("Invalid JSON."); }

        var departureGate       = body.TryGetProperty("departureGate",       out var gEl) ? gEl.GetString() : null;
        var aircraftRegistration = body.TryGetProperty("aircraftRegistration", out var rEl) ? rEl.GetString() : null;

        var command = new SetInventoryOperationalDataCommand(inventoryId, departureGate, aircraftRegistration);

        try
        {
            await _setOperationalDataHandler.HandleAsync(command, ct);
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
    }

    private static int CalculateDurationMinutes(FlightInventory inv)
    {
        var dep    = inv.DepartureTimeUtc ?? inv.DepartureTime;
        var arr    = inv.ArrivalTimeUtc   ?? inv.ArrivalTime;
        var offset = inv.ArrivalDayOffsetUtc ?? inv.ArrivalDayOffset;
        return arr.Hour * 60 + arr.Minute + offset * 1440 - (dep.Hour * 60 + dep.Minute);
    }
}
