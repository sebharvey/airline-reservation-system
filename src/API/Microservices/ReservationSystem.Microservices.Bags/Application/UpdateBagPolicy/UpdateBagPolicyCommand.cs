namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPolicy;

public sealed record UpdateBagPolicyCommand(
    Guid PolicyId,
    int FreeBagsIncluded,
    int MaxWeightKgPerBag,
    bool IsActive);
