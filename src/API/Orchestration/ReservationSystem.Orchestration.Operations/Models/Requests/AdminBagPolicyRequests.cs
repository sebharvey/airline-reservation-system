namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminCreateBagPolicyRequest
{
    public string CabinCode { get; init; } = string.Empty;
    public int FreeBagsIncluded { get; init; }
    public int MaxWeightKgPerBag { get; init; }
}

public sealed class AdminUpdateBagPolicyRequest
{
    public int FreeBagsIncluded { get; init; }
    public int MaxWeightKgPerBag { get; init; }
    public bool IsActive { get; init; }
}
