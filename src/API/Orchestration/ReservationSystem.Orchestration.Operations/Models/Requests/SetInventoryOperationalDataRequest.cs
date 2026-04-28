namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class SetInventoryOperationalDataRequest
{
    public string? DepartureGate { get; init; }
    public string? AircraftRegistration { get; init; }
}
