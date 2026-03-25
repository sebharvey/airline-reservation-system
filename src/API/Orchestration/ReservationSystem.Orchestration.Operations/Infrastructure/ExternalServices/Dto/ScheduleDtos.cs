namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class CreateScheduleDto
{
    public Guid ScheduleId { get; init; }
    public IReadOnlyList<string> OperatingDates { get; init; } = [];
}

public sealed class UpdateScheduleDto
{
    public Guid ScheduleId { get; init; }
    public int FlightsCreated { get; init; }
}
