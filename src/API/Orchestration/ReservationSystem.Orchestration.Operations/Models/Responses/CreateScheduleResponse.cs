namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class CreateScheduleResponse
{
    public Guid ScheduleId { get; init; }
    public int FlightsCreated { get; init; }
}
