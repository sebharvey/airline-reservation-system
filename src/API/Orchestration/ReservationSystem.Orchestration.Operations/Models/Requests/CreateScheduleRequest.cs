namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class CreateScheduleRequest
{
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public int DaysOfWeek { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public string ValidFrom { get; init; } = string.Empty;
    public string ValidTo { get; init; } = string.Empty;
    public IReadOnlyList<CabinRequest> Cabins { get; init; } = [];
}

public sealed class CabinRequest
{
    public string CabinCode { get; init; } = string.Empty;
    public int TotalSeats { get; init; }
    public IReadOnlyList<FareRequest> Fares { get; init; } = [];
}

public sealed class FareRequest
{
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public decimal ChangeFeeAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public int? PointsPrice { get; init; }
    public decimal? PointsTaxes { get; init; }
}
