namespace ReservationSystem.Orchestration.Operations.Application.ImportSsim;

public sealed record ImportSsimCommand(string SsimText, string CreatedBy, Guid ScheduleGroupId);
