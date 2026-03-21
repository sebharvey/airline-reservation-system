namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPolicy;

public sealed record UpdateBagPolicyCommand(
    Guid PolicyId,
    string CabinCode,
    int FreeBagsIncluded,
    int MaxWeightKgPerBag,
    bool IsActive);
