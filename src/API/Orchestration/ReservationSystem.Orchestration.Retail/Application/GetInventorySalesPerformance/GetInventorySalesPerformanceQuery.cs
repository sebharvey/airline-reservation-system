namespace ReservationSystem.Orchestration.Retail.Application.GetInventorySalesPerformance;

public sealed record GetInventorySalesPerformanceQuery(
    Guid InventoryId,
    int Days = 21);
