namespace ReservationSystem.Microservices.Offer.Domain.Entities;

/// <summary>
/// Seat inventory for a single cabin on a flight. Stored as JSON within FlightInventory.
/// </summary>
public sealed class CabinInventory
{
    public string CabinCode { get; private set; } = string.Empty;
    public int TotalSeats { get; private set; }
    public int SeatsSold { get; private set; }
    public int SeatsHeld { get; private set; }
    public int SeatsAvailable => TotalSeats - SeatsSold - SeatsHeld;

    private CabinInventory() { }

    internal static CabinInventory Create(string cabinCode, int totalSeats) =>
        new() { CabinCode = cabinCode, TotalSeats = totalSeats };

    internal static CabinInventory Reconstitute(string cabinCode, int totalSeats, int seatsSold, int seatsHeld) =>
        new() { CabinCode = cabinCode, TotalSeats = totalSeats, SeatsSold = seatsSold, SeatsHeld = seatsHeld };

    internal void Hold(int count) { SeatsHeld += count; }
    internal void Sell(int count) { SeatsHeld -= count; SeatsSold += count; }
    internal void ReleaseHeld(int count) { SeatsHeld -= count; }
    internal void ReleaseSold(int count) { SeatsSold -= count; }
}

public sealed class FlightInventory
{
    private readonly List<CabinInventory> _cabins = [];

    public Guid InventoryId { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public DateOnly DepartureDate { get; private set; }
    public TimeOnly DepartureTime { get; private set; }
    public TimeOnly ArrivalTime { get; private set; }
    public int ArrivalDayOffset { get; private set; }
    public string Origin { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public string AircraftType { get; private set; } = string.Empty;
    public IReadOnlyList<CabinInventory> Cabins => _cabins.AsReadOnly();
    public int TotalSeats { get; private set; }
    public int SeatsAvailable { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FlightInventory() { }

    public static FlightInventory Create(
        string flightNumber, DateOnly departureDate, TimeOnly departureTime, TimeOnly arrivalTime,
        int arrivalDayOffset, string origin, string destination, string aircraftType,
        IReadOnlyList<(string CabinCode, int TotalSeats)> cabins)
    {
        var cabinList = cabins.Select(c => CabinInventory.Create(c.CabinCode, c.TotalSeats)).ToList();
        var totalSeats = cabinList.Sum(c => c.TotalSeats);
        var inv = new FlightInventory
        {
            InventoryId = Guid.NewGuid(),
            FlightNumber = flightNumber,
            DepartureDate = departureDate,
            DepartureTime = departureTime,
            ArrivalTime = arrivalTime,
            ArrivalDayOffset = arrivalDayOffset,
            Origin = origin,
            Destination = destination,
            AircraftType = aircraftType,
            TotalSeats = totalSeats,
            SeatsAvailable = totalSeats,
            Status = InventoryStatus.Active
        };
        inv._cabins.AddRange(cabinList);
        return inv;
    }

    public static FlightInventory Reconstitute(
        Guid inventoryId, string flightNumber, DateOnly departureDate, TimeOnly departureTime,
        TimeOnly arrivalTime, int arrivalDayOffset, string origin, string destination,
        string aircraftType, IReadOnlyList<CabinInventory> cabins,
        int totalSeats, int seatsAvailable, string status, DateTime createdAt, DateTime updatedAt)
    {
        var inv = new FlightInventory
        {
            InventoryId = inventoryId, FlightNumber = flightNumber, DepartureDate = departureDate,
            DepartureTime = departureTime, ArrivalTime = arrivalTime, ArrivalDayOffset = arrivalDayOffset,
            Origin = origin, Destination = destination, AircraftType = aircraftType,
            TotalSeats = totalSeats, SeatsAvailable = seatsAvailable, Status = status,
            CreatedAt = createdAt, UpdatedAt = updatedAt
        };
        inv._cabins.AddRange(cabins);
        return inv;
    }

    private CabinInventory GetCabin(string cabinCode) =>
        _cabins.FirstOrDefault(c => c.CabinCode == cabinCode)
        ?? throw new ArgumentException($"Cabin {cabinCode} not found on inventory {InventoryId}.");

    public void HoldSeats(string cabinCode, int count)
    {
        GetCabin(cabinCode).Hold(count);
        SeatsAvailable -= count;
    }

    public void SellSeats(string cabinCode, int count) => GetCabin(cabinCode).Sell(count);

    public void ReleaseHeld(string cabinCode, int count)
    {
        GetCabin(cabinCode).ReleaseHeld(count);
        SeatsAvailable += count;
    }

    public void ReleaseSold(string cabinCode, int count)
    {
        GetCabin(cabinCode).ReleaseSold(count);
        SeatsAvailable += count;
    }

    public void Cancel() { Status = InventoryStatus.Cancelled; SeatsAvailable = 0; }
}

public static class InventoryStatus
{
    public const string Active = "Active";
    public const string Cancelled = "Cancelled";
}

public sealed class Fare
{
    public Guid FareId { get; private set; }
    public Guid InventoryId { get; private set; }
    public string FareBasisCode { get; private set; } = string.Empty;
    public string? FareFamily { get; private set; }
    public string CabinCode { get; private set; } = string.Empty;
    public string BookingClass { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal BaseFareAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool IsRefundable { get; private set; }
    public bool IsChangeable { get; private set; }
    public decimal ChangeFeeAmount { get; private set; }
    public decimal CancellationFeeAmount { get; private set; }
    public int? PointsPrice { get; private set; }
    public decimal? PointsTaxes { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Fare() { }

    public static Fare Create(
        Guid inventoryId, string fareBasisCode, string? fareFamily, string cabinCode,
        string bookingClass, string currencyCode, decimal baseFareAmount, decimal taxAmount,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        int? pointsPrice, decimal? pointsTaxes, DateTime validFrom, DateTime validTo)
    {
        return new Fare
        {
            FareId = Guid.NewGuid(),
            InventoryId = inventoryId, FareBasisCode = fareBasisCode, FareFamily = fareFamily,
            CabinCode = cabinCode, BookingClass = bookingClass, CurrencyCode = currencyCode,
            BaseFareAmount = baseFareAmount, TaxAmount = taxAmount,
            TotalAmount = baseFareAmount + taxAmount,
            IsRefundable = isRefundable, IsChangeable = isChangeable,
            ChangeFeeAmount = changeFeeAmount, CancellationFeeAmount = cancellationFeeAmount,
            PointsPrice = pointsPrice, PointsTaxes = pointsTaxes,
            ValidFrom = validFrom, ValidTo = validTo,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
    }

    public static Fare Reconstitute(
        Guid fareId, Guid inventoryId, string fareBasisCode, string? fareFamily,
        string cabinCode, string bookingClass, string currencyCode,
        decimal baseFareAmount, decimal taxAmount, decimal totalAmount,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        int? pointsPrice, decimal? pointsTaxes, DateTime validFrom, DateTime validTo,
        DateTime createdAt, DateTime updatedAt)
    {
        return new Fare
        {
            FareId = fareId, InventoryId = inventoryId, FareBasisCode = fareBasisCode,
            FareFamily = fareFamily, CabinCode = cabinCode, BookingClass = bookingClass,
            CurrencyCode = currencyCode, BaseFareAmount = baseFareAmount, TaxAmount = taxAmount,
            TotalAmount = totalAmount, IsRefundable = isRefundable, IsChangeable = isChangeable,
            ChangeFeeAmount = changeFeeAmount, CancellationFeeAmount = cancellationFeeAmount,
            PointsPrice = pointsPrice, PointsTaxes = pointsTaxes,
            ValidFrom = validFrom, ValidTo = validTo,
            CreatedAt = createdAt, UpdatedAt = updatedAt
        };
    }
}

public sealed class StoredOffer
{
    public Guid OfferId { get; private set; }
    public Guid InventoryId { get; private set; }
    public Guid FareId { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public DateOnly DepartureDate { get; private set; }
    public TimeOnly DepartureTime { get; private set; }
    public TimeOnly ArrivalTime { get; private set; }
    public int ArrivalDayOffset { get; private set; }
    public string Origin { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public string AircraftType { get; private set; } = string.Empty;
    public string CabinCode { get; private set; } = string.Empty;
    public string FareBasisCode { get; private set; } = string.Empty;
    public string? FareFamily { get; private set; }
    public string BookingClass { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = string.Empty;
    public decimal BaseFareAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool IsRefundable { get; private set; }
    public bool IsChangeable { get; private set; }
    public decimal ChangeFeeAmount { get; private set; }
    public decimal CancellationFeeAmount { get; private set; }
    public int? PointsPrice { get; private set; }
    public decimal? PointsTaxes { get; private set; }
    public int SeatsAvailable { get; private set; }
    public string BookingType { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsConsumed { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private StoredOffer() { }

    public static StoredOffer Create(FlightInventory inventory, Fare fare, string bookingType)
    {
        var now = DateTime.UtcNow;
        var cabin = inventory.Cabins.FirstOrDefault(c => c.CabinCode == fare.CabinCode);
        return new StoredOffer
        {
            OfferId = Guid.NewGuid(),
            InventoryId = inventory.InventoryId, FareId = fare.FareId,
            FlightNumber = inventory.FlightNumber, DepartureDate = inventory.DepartureDate,
            DepartureTime = inventory.DepartureTime, ArrivalTime = inventory.ArrivalTime,
            ArrivalDayOffset = inventory.ArrivalDayOffset,
            Origin = inventory.Origin, Destination = inventory.Destination,
            AircraftType = inventory.AircraftType, CabinCode = fare.CabinCode,
            FareBasisCode = fare.FareBasisCode, FareFamily = fare.FareFamily,
            BookingClass = fare.BookingClass, CurrencyCode = fare.CurrencyCode,
            BaseFareAmount = fare.BaseFareAmount, TaxAmount = fare.TaxAmount,
            TotalAmount = fare.TotalAmount, IsRefundable = fare.IsRefundable,
            IsChangeable = fare.IsChangeable, ChangeFeeAmount = fare.ChangeFeeAmount,
            CancellationFeeAmount = fare.CancellationFeeAmount,
            PointsPrice = fare.PointsPrice, PointsTaxes = fare.PointsTaxes,
            SeatsAvailable = cabin?.SeatsAvailable ?? 0,
            BookingType = bookingType,
            CreatedAt = now, ExpiresAt = now.AddMinutes(60), IsConsumed = false,
            UpdatedAt = now
        };
    }

    public static StoredOffer Reconstitute(
        Guid offerId, Guid inventoryId, Guid fareId, string flightNumber,
        DateOnly departureDate, TimeOnly departureTime, TimeOnly arrivalTime, int arrivalDayOffset,
        string origin, string destination, string aircraftType, string cabinCode,
        string fareBasisCode, string? fareFamily, string bookingClass, string currencyCode,
        decimal baseFareAmount, decimal taxAmount, decimal totalAmount,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        int? pointsPrice, decimal? pointsTaxes, int seatsAvailable, string bookingType,
        DateTime createdAt, DateTime expiresAt, bool isConsumed, DateTime updatedAt)
    {
        return new StoredOffer
        {
            OfferId = offerId, InventoryId = inventoryId, FareId = fareId,
            FlightNumber = flightNumber, DepartureDate = departureDate,
            DepartureTime = departureTime, ArrivalTime = arrivalTime,
            ArrivalDayOffset = arrivalDayOffset, Origin = origin, Destination = destination,
            AircraftType = aircraftType, CabinCode = cabinCode,
            FareBasisCode = fareBasisCode, FareFamily = fareFamily,
            BookingClass = bookingClass, CurrencyCode = currencyCode,
            BaseFareAmount = baseFareAmount, TaxAmount = taxAmount, TotalAmount = totalAmount,
            IsRefundable = isRefundable, IsChangeable = isChangeable,
            ChangeFeeAmount = changeFeeAmount, CancellationFeeAmount = cancellationFeeAmount,
            PointsPrice = pointsPrice, PointsTaxes = pointsTaxes,
            SeatsAvailable = seatsAvailable, BookingType = bookingType,
            CreatedAt = createdAt, ExpiresAt = expiresAt, IsConsumed = isConsumed, UpdatedAt = updatedAt
        };
    }

    public void Consume() { IsConsumed = true; UpdatedAt = DateTime.UtcNow; }
}

public sealed class FareRule
{
    public Guid FareRuleId { get; private set; }
    public string RuleType { get; private set; } = "Money";
    public string? FlightNumber { get; private set; }
    public string FareBasisCode { get; private set; } = string.Empty;
    public string? FareFamily { get; private set; }
    public string CabinCode { get; private set; } = string.Empty;
    public string BookingClass { get; private set; } = string.Empty;
    public string? CurrencyCode { get; private set; }
    public decimal? MinAmount { get; private set; }
    public decimal? MaxAmount { get; private set; }
    public decimal? TaxAmount { get; private set; }
    public int? MinPoints { get; private set; }
    public int? MaxPoints { get; private set; }
    public decimal? PointsTaxes { get; private set; }
    public bool IsRefundable { get; private set; }
    public bool IsChangeable { get; private set; }
    public decimal ChangeFeeAmount { get; private set; }
    public decimal CancellationFeeAmount { get; private set; }
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FareRule() { }

    public static FareRule Create(
        string ruleType, string? flightNumber, string fareBasisCode, string? fareFamily,
        string cabinCode, string bookingClass, string? currencyCode,
        decimal? minAmount, decimal? maxAmount, decimal? taxAmount,
        int? minPoints, int? maxPoints, decimal? pointsTaxes,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        DateTime? validFrom, DateTime? validTo)
    {
        return new FareRule
        {
            FareRuleId = Guid.NewGuid(),
            RuleType = ruleType, FlightNumber = flightNumber,
            FareBasisCode = fareBasisCode, FareFamily = fareFamily,
            CabinCode = cabinCode, BookingClass = bookingClass, CurrencyCode = currencyCode,
            MinAmount = minAmount, MaxAmount = maxAmount, TaxAmount = taxAmount,
            MinPoints = minPoints, MaxPoints = maxPoints, PointsTaxes = pointsTaxes,
            IsRefundable = isRefundable, IsChangeable = isChangeable,
            ChangeFeeAmount = changeFeeAmount, CancellationFeeAmount = cancellationFeeAmount,
            ValidFrom = validFrom, ValidTo = validTo,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
    }

    public static FareRule Reconstitute(
        Guid fareRuleId, string ruleType, string? flightNumber, string fareBasisCode,
        string? fareFamily, string cabinCode, string bookingClass, string? currencyCode,
        decimal? minAmount, decimal? maxAmount, decimal? taxAmount,
        int? minPoints, int? maxPoints, decimal? pointsTaxes,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        DateTime? validFrom, DateTime? validTo, DateTime createdAt, DateTime updatedAt)
    {
        return new FareRule
        {
            FareRuleId = fareRuleId, RuleType = ruleType, FlightNumber = flightNumber,
            FareBasisCode = fareBasisCode, FareFamily = fareFamily,
            CabinCode = cabinCode, BookingClass = bookingClass, CurrencyCode = currencyCode,
            MinAmount = minAmount, MaxAmount = maxAmount, TaxAmount = taxAmount,
            MinPoints = minPoints, MaxPoints = maxPoints, PointsTaxes = pointsTaxes,
            IsRefundable = isRefundable, IsChangeable = isChangeable,
            ChangeFeeAmount = changeFeeAmount, CancellationFeeAmount = cancellationFeeAmount,
            ValidFrom = validFrom, ValidTo = validTo,
            CreatedAt = createdAt, UpdatedAt = updatedAt
        };
    }

    public void Update(
        string ruleType, string? flightNumber, string fareBasisCode, string? fareFamily,
        string cabinCode, string bookingClass, string? currencyCode,
        decimal? minAmount, decimal? maxAmount, decimal? taxAmount,
        int? minPoints, int? maxPoints, decimal? pointsTaxes,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        DateTime? validFrom, DateTime? validTo)
    {
        RuleType = ruleType; FlightNumber = flightNumber;
        FareBasisCode = fareBasisCode; FareFamily = fareFamily;
        CabinCode = cabinCode; BookingClass = bookingClass; CurrencyCode = currencyCode;
        MinAmount = minAmount; MaxAmount = maxAmount; TaxAmount = taxAmount;
        MinPoints = minPoints; MaxPoints = maxPoints; PointsTaxes = pointsTaxes;
        IsRefundable = isRefundable; IsChangeable = isChangeable;
        ChangeFeeAmount = changeFeeAmount; CancellationFeeAmount = cancellationFeeAmount;
        ValidFrom = validFrom; ValidTo = validTo;
        UpdatedAt = DateTime.UtcNow;
    }
}
