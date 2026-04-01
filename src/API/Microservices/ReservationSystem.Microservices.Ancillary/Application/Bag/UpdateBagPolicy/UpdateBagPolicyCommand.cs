namespace ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPolicy;

public sealed record UpdateBagPolicyCommand(
    Guid PolicyId,
    int FreeBagsIncluded,
    int MaxWeightKgPerBag,
    bool IsActive);
