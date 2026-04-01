namespace ReservationSystem.Microservices.Ancillary.Application.Bag.CreateBagPolicy;

public sealed record CreateBagPolicyCommand(
    string CabinCode,
    int FreeBagsIncluded,
    int MaxWeightKgPerBag);
