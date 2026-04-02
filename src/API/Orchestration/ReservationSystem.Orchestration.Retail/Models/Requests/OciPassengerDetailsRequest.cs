namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class OciPassengerDetailsRequest
{
    public List<OciPassengerDetail> Passengers { get; init; } = [];
}

public sealed class OciPassengerDetail
{
    public string PassengerId { get; init; } = string.Empty;
    public OciTravelDocumentDetail? TravelDocument { get; init; }
}

public sealed class OciTravelDocumentDetail
{
    public string Type { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string IssuingCountry { get; init; } = string.Empty;
    public string Nationality { get; init; } = string.Empty;
    public string IssueDate { get; init; } = string.Empty;
    public string ExpiryDate { get; init; } = string.Empty;
}
