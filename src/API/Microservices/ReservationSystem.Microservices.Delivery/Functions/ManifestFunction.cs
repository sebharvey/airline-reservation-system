using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.WriteManifest;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class ManifestFunction
{
    private readonly WriteManifestHandler _writeHandler;
    private readonly ILogger<ManifestFunction> _logger;

    public ManifestFunction(WriteManifestHandler writeHandler, ILogger<ManifestFunction> logger)
    {
        _writeHandler = writeHandler;
        _logger = logger;
    }

    // POST /v1/manifest
    [Function("WriteManifest")]
    [OpenApiOperation(operationId: "WriteManifest", tags: new[] { "Manifest" }, Summary = "Write passenger manifest entries for a flight segment")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(WriteManifestRequest), Required = true, Description = "Manifest write request: booking reference, flight details, and one entry per passenger")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(WriteManifestResponse), Description = "Created — returns written and skipped counts")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> WriteManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/manifest")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<WriteManifestRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request.BookingReference))
            return await req.BadRequestAsync("bookingReference is required.");

        if (request.Entries.Count == 0)
            return await req.BadRequestAsync("entries must contain at least one entry.");

        try
        {
            var result = await _writeHandler.HandleAsync(request, cancellationToken);
            return await req.CreatedAsync("/v1/manifest", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write manifest for booking {BookingRef}", request.BookingReference);
            return await req.InternalServerErrorAsync();
        }
    }
}
