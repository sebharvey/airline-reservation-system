namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class OciBagsRequest
{
    public List<OciBagItem> BagSelections { get; init; } = [];
    public OciPaymentDetails? Payment { get; init; }
}

public sealed class OciBagItem
{
    public string PassengerId { get; init; } = string.Empty;
    public string SegmentRef { get; init; } = string.Empty;
    public string BagOfferId { get; init; } = string.Empty;
    public int AdditionalBags { get; init; }
}

public sealed class OciPaymentDetails
{
    public string Method { get; init; } = string.Empty;
    public string CardNumber { get; init; } = string.Empty;
    public string ExpiryDate { get; init; } = string.Empty;
    public string Cvv { get; init; } = string.Empty;
    public string CardholderName { get; init; } = string.Empty;
}
