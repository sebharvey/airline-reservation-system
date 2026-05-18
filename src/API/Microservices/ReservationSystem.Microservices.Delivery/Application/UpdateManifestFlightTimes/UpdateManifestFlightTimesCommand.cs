namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestFlightTimes;

public sealed record UpdateManifestFlightTimesCommand(
    string FlightNumber,
    string DepartureDate,
    string NewDepartureTime,
    string NewArrivalTime);
