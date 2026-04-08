using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.Ssim;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.ImportSsim;

/// <summary>
/// Handles SSIM file import by:
///   1. Parsing the SSIM file using <see cref="SsimParser"/> in the Operations API.
///   2. Converting the parsed records into the season schedule JSON payload format.
///   3. Forwarding the payload to the Schedule MS <c>POST /v1/schedules</c> endpoint.
/// </summary>
public sealed class ImportSsimHandler
{
    private readonly ScheduleServiceClient _scheduleServiceClient;
    private readonly ILogger<ImportSsimHandler> _logger;

    public ImportSsimHandler(
        ScheduleServiceClient scheduleServiceClient,
        ILogger<ImportSsimHandler> logger)
    {
        _scheduleServiceClient = scheduleServiceClient;
        _logger = logger;
    }

    public async Task<ImportSsimResponse> HandleAsync(
        ImportSsimCommand command,
        CancellationToken cancellationToken = default)
    {
        var parsed = SsimParser.Parse(command.SsimText, command.CreatedBy);

        if (parsed.Records.Count == 0)
            throw new ArgumentException("No valid Type 3 scheduled-passenger records found in the SSIM file.");

        _logger.LogInformation(
            "SSIM parsed: carrier={Carrier}, season={Season}, records={Count}",
            parsed.CarrierCode, parsed.SeasonCode, parsed.Records.Count);

        // Build the season schedule JSON payload to send to Schedule MS.
        var payload = BuildSchedulePayload(parsed, command.ScheduleGroupId);

        return await _scheduleServiceClient.ImportSchedulesAsync(payload, cancellationToken);
    }

    private static object BuildSchedulePayload(SsimParseResult parsed, Guid scheduleGroupId)
    {
        var schedules = parsed.Records.Select(r => new
        {
            r.FlightNumber,
            r.Origin,
            r.Destination,
            r.DepartureTime,
            r.ArrivalTime,
            r.ArrivalDayOffset,
            r.DepartureTimeUtc,
            r.ArrivalTimeUtc,
            r.ArrivalDayOffsetUtc,
            r.DaysOfWeek,
            r.AircraftType,
            r.ValidFrom,
            r.ValidTo,
            r.CreatedBy
        }).ToList();

        return new
        {
            scheduleGroupId,
            header = new
            {
                standard = "IATA",
                airlineCode = parsed.CarrierCode,
                seasonStart = parsed.SeasonStart,
                seasonEnd   = parsed.SeasonEnd,
                fileType    = "SCHED",
                fileCreationDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
            },
            carriers = new[]
            {
                new
                {
                    airlineCode = parsed.CarrierCode,
                    seasonCode  = parsed.SeasonCode,
                    validFrom   = parsed.SeasonStart,
                    validTo     = parsed.SeasonEnd,
                    schedules
                }
            },
            recordCount = schedules.Count
        };
    }
}
