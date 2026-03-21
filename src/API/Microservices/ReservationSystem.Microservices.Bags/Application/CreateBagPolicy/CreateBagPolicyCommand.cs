namespace ReservationSystem.Microservices.Bags.Application.CreateBagPolicy;

public sealed record CreateBagPolicyCommand(
    string CabinCode,
    int FreeBagsIncluded,
    int MaxWeightKgPerBag);
