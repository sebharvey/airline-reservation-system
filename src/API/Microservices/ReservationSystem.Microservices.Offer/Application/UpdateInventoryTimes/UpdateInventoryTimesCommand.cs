namespace ReservationSystem.Microservices.Offer.Application.UpdateInventoryTimes;

public sealed record UpdateInventoryTimesCommand(
    string FlightNumber,
    string DepartureDate,
    string NewDepartureTime,
    string NewArrivalTime,
    int NewArrivalDayOffset,
    string? NewDepartureTimeUtc,
    string? NewArrivalTimeUtc,
    int? NewArrivalDayOffsetUtc);
