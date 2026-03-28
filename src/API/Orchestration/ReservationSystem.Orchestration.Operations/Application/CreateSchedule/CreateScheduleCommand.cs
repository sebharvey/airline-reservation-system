using ReservationSystem.Orchestration.Operations.Models.Requests;

namespace ReservationSystem.Orchestration.Operations.Application.CreateSchedule;

public sealed record CreateScheduleCommand(
    string FlightNumber,
    string Origin,
    string Destination,
    string DepartureTime,
    string ArrivalTime,
    int ArrivalDayOffset,
    int DaysOfWeek,
    string AircraftType,
    string ValidFrom,
    string ValidTo,
    IReadOnlyList<CabinRequest> Cabins);
