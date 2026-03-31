namespace ReservationSystem.Orchestration.Operations.Application.ImportSchedulesToInventory;

public sealed record ImportSchedulesToInventoryCommand(Guid? ScheduleGroupId = null, DateTime? ToDate = null);
