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
    public TimeOnly? DepartureTimeUtc { get; private set; }
    public TimeOnly? ArrivalTimeUtc { get; private set; }
    public int? ArrivalDayOffsetUtc { get; private set; }
    public IReadOnlyList<CabinInventory> Cabins => _cabins.AsReadOnly();
    public int TotalSeats { get; private set; }
    public int SeatsAvailable { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FlightInventory() { }

    public static FlightInventory Create(
        string flightNumber, DateOnly departureDate, TimeOnly departureTime, TimeOnly arrivalTime,
        int arrivalDayOffset, string origin, string destination, string aircraftType,
        IReadOnlyList<(string CabinCode, int TotalSeats)> cabins,
        TimeOnly? departureTimeUtc = null, TimeOnly? arrivalTimeUtc = null, int? arrivalDayOffsetUtc = null)
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
            Status = InventoryStatus.Active,
            DepartureTimeUtc = departureTimeUtc,
            ArrivalTimeUtc = arrivalTimeUtc,
            ArrivalDayOffsetUtc = arrivalDayOffsetUtc
        };
        inv._cabins.AddRange(cabinList);
        return inv;
    }

    public static FlightInventory Reconstitute(
        Guid inventoryId, string flightNumber, DateOnly departureDate, TimeOnly departureTime,
        TimeOnly arrivalTime, int arrivalDayOffset, string origin, string destination,
        string aircraftType, IReadOnlyList<CabinInventory> cabins,
        int totalSeats, int seatsAvailable, string status, DateTimeOffset createdAt, DateTimeOffset updatedAt,
        TimeOnly? departureTimeUtc = null, TimeOnly? arrivalTimeUtc = null, int? arrivalDayOffsetUtc = null)
    {
        var inv = new FlightInventory
        {
            InventoryId = inventoryId, FlightNumber = flightNumber, DepartureDate = departureDate,
            DepartureTime = departureTime, ArrivalTime = arrivalTime, ArrivalDayOffset = arrivalDayOffset,
            Origin = origin, Destination = destination, AircraftType = aircraftType,
            TotalSeats = totalSeats, SeatsAvailable = seatsAvailable, Status = status,
            CreatedAt = createdAt, UpdatedAt = updatedAt,
            DepartureTimeUtc = departureTimeUtc, ArrivalTimeUtc = arrivalTimeUtc, ArrivalDayOffsetUtc = arrivalDayOffsetUtc
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

    public void ChangeAircraftType(string newAircraftType) { AircraftType = newAircraftType; }
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
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Fare() { }

    public static Fare Create(
        Guid inventoryId, string fareBasisCode, string? fareFamily, string cabinCode,
        string bookingClass, string currencyCode, decimal baseFareAmount, decimal taxAmount,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        int? pointsPrice, decimal? pointsTaxes, DateTimeOffset validFrom, DateTimeOffset validTo)
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
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Fare Reconstitute(
        Guid fareId, Guid inventoryId, string fareBasisCode, string? fareFamily,
        string cabinCode, string bookingClass, string currencyCode,
        decimal baseFareAmount, decimal taxAmount, decimal totalAmount,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        int? pointsPrice, decimal? pointsTaxes, DateTimeOffset validFrom, DateTimeOffset validTo,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
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

/// <summary>
/// A single tax line within a stored offer item, copied from FareRule.TaxLines at reprice time.
/// </summary>
public sealed class TaxLineItem
{
    public string Code { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// A single cabin fare entry within a stored offer's FaresInfo JSON payload.
/// Each item has its own OfferId so the basket flow can reference a specific fare.
/// TaxLines is null until the offer is repriced via POST /v1/offers/{offerId}/reprice.
/// </summary>
public sealed class StoredOfferItem
{
    public Guid OfferId { get; init; }
    public Guid FareRuleId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public decimal ChangeFeeAmount { get; init; }
    public decimal CancellationFeeAmount { get; init; }
    public int? PointsPrice { get; init; }
    public decimal? PointsTaxes { get; init; }
    public int SeatsAvailable { get; init; }
    public string BookingType { get; init; } = string.Empty;
    public IReadOnlyList<TaxLineItem>? TaxLines { get; init; }
}

/// <summary>
/// One flight's fare entries within a stored offer's FaresInfo JSON payload.
/// </summary>
public sealed class StoredOfferInventoryEntry
{
    public Guid InventoryId { get; init; }
    public bool Validated { get; init; } = false;
    public IReadOnlyList<StoredOfferItem> Offers { get; init; } = [];
}

/// <summary>
/// Fare data stored as JSON in the StoredOffer.FaresInfo column.
/// Contains one entry per matching flight; flight details are authoritative in
/// FlightInventory and derived from InventoryId at read time.
/// </summary>
public sealed class StoredOfferFaresInfo
{
    public IReadOnlyList<StoredOfferInventoryEntry> Inventories { get; init; } = [];
}

public sealed class StoredOffer
{
    public Guid StoredOfferId { get; private set; }
    public Guid SessionId { get; private set; }
    public string FaresInfo { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    private StoredOffer() { }

    public static StoredOffer Create(
        IReadOnlyList<(FlightInventory Inventory, IReadOnlyList<(Fare Fare, Guid FareRuleId)> Fares)> inventoryFares,
        string bookingType,
        Guid sessionId)
    {
        var entries = inventoryFares.Select(ivf =>
        {
            var items = ivf.Fares.Select(f =>
            {
                var cabin = ivf.Inventory.Cabins.FirstOrDefault(c => c.CabinCode == f.Fare.CabinCode);
                return new StoredOfferItem
                {
                    OfferId               = Guid.NewGuid(),
                    FareRuleId            = f.FareRuleId,
                    CabinCode             = f.Fare.CabinCode,
                    FareBasisCode         = f.Fare.FareBasisCode,
                    FareFamily            = f.Fare.FareFamily,
                    CurrencyCode          = f.Fare.CurrencyCode,
                    BaseFareAmount        = f.Fare.BaseFareAmount,
                    TaxAmount             = f.Fare.TaxAmount,
                    TotalAmount           = f.Fare.TotalAmount,
                    IsRefundable          = f.Fare.IsRefundable,
                    IsChangeable          = f.Fare.IsChangeable,
                    ChangeFeeAmount       = f.Fare.ChangeFeeAmount,
                    CancellationFeeAmount = f.Fare.CancellationFeeAmount,
                    PointsPrice           = f.Fare.PointsPrice,
                    PointsTaxes           = f.Fare.PointsTaxes,
                    SeatsAvailable        = cabin?.SeatsAvailable ?? 0,
                    BookingType           = bookingType
                };
            }).ToList();

            return new StoredOfferInventoryEntry
            {
                InventoryId = ivf.Inventory.InventoryId,
                Validated   = false,
                Offers      = items
            };
        }).ToList();

        var faresInfo = new StoredOfferFaresInfo { Inventories = entries };

        return new StoredOffer
        {
            StoredOfferId = Guid.NewGuid(),
            SessionId     = sessionId,
            FaresInfo     = System.Text.Json.JsonSerializer.Serialize(faresInfo, JsonOpts),
            ExpiresAt     = DateTimeOffset.UtcNow.AddMinutes(60)
        };
    }

    public static StoredOffer Reconstitute(
        Guid storedOfferId, Guid sessionId, string faresInfo,
        DateTimeOffset createdAt, DateTimeOffset expiresAt, DateTimeOffset updatedAt)
    {
        return new StoredOffer
        {
            StoredOfferId = storedOfferId,
            SessionId     = sessionId,
            FaresInfo     = faresInfo,
            CreatedAt     = createdAt,
            ExpiresAt     = expiresAt,
            UpdatedAt     = updatedAt
        };
    }

    public StoredOfferFaresInfo GetFaresInfo() =>
        System.Text.Json.JsonSerializer.Deserialize<StoredOfferFaresInfo>(FaresInfo,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
}

public sealed class FareFamily
{
    public Guid FareFamilyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FareFamily() { }

    public static FareFamily Create(string name, string? description, int displayOrder) =>
        new()
        {
            FareFamilyId = Guid.NewGuid(),
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    public static FareFamily Reconstitute(
        Guid fareFamilyId, string name, string? description, int displayOrder,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new()
        {
            FareFamilyId = fareFamilyId,
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

    public void Update(string name, string? description, int displayOrder)
    {
        Name = name;
        Description = description;
        DisplayOrder = displayOrder;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
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
    public int? MinPoints { get; private set; }
    public int? MaxPoints { get; private set; }
    public decimal? PointsTaxes { get; private set; }
    public string? TaxLines { get; private set; }
    public bool IsRefundable { get; private set; }
    public bool IsChangeable { get; private set; }
    public decimal ChangeFeeAmount { get; private set; }
    public decimal CancellationFeeAmount { get; private set; }
    public bool IsPrivate { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FareRule() { }

    public static FareRule Create(
        string ruleType, string? flightNumber, string fareBasisCode, string? fareFamily,
        string cabinCode, string bookingClass, string? currencyCode,
        decimal? minAmount, decimal? maxAmount,
        int? minPoints, int? maxPoints, decimal? pointsTaxes,
        string? taxLines,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        bool isPrivate,
        DateTimeOffset? validFrom, DateTimeOffset? validTo)
    {
        return new FareRule
        {
            FareRuleId = Guid.NewGuid(),
            RuleType = ruleType, FlightNumber = flightNumber,
            FareBasisCode = fareBasisCode, FareFamily = fareFamily,
            CabinCode = cabinCode, BookingClass = bookingClass, CurrencyCode = currencyCode,
            MinAmount = minAmount, MaxAmount = maxAmount,
            MinPoints = minPoints, MaxPoints = maxPoints, PointsTaxes = pointsTaxes,
            TaxLines = taxLines,
            IsRefundable = isRefundable, IsChangeable = isChangeable,
            ChangeFeeAmount = changeFeeAmount, CancellationFeeAmount = cancellationFeeAmount,
            IsPrivate = isPrivate,
            ValidFrom = validFrom, ValidTo = validTo,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static FareRule Reconstitute(
        Guid fareRuleId, string ruleType, string? flightNumber, string fareBasisCode,
        string? fareFamily, string cabinCode, string bookingClass, string? currencyCode,
        decimal? minAmount, decimal? maxAmount,
        int? minPoints, int? maxPoints, decimal? pointsTaxes,
        string? taxLines,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        bool isPrivate,
        DateTimeOffset? validFrom, DateTimeOffset? validTo, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        return new FareRule
        {
            FareRuleId = fareRuleId, RuleType = ruleType, FlightNumber = flightNumber,
            FareBasisCode = fareBasisCode, FareFamily = fareFamily,
            CabinCode = cabinCode, BookingClass = bookingClass, CurrencyCode = currencyCode,
            MinAmount = minAmount, MaxAmount = maxAmount,
            MinPoints = minPoints, MaxPoints = maxPoints, PointsTaxes = pointsTaxes,
            TaxLines = taxLines,
            IsRefundable = isRefundable, IsChangeable = isChangeable,
            ChangeFeeAmount = changeFeeAmount, CancellationFeeAmount = cancellationFeeAmount,
            IsPrivate = isPrivate,
            ValidFrom = validFrom, ValidTo = validTo,
            CreatedAt = createdAt, UpdatedAt = updatedAt
        };
    }

    public void Update(
        string ruleType, string? flightNumber, string fareBasisCode, string? fareFamily,
        string cabinCode, string bookingClass, string? currencyCode,
        decimal? minAmount, decimal? maxAmount,
        int? minPoints, int? maxPoints, decimal? pointsTaxes,
        string? taxLines,
        bool isRefundable, bool isChangeable, decimal changeFeeAmount, decimal cancellationFeeAmount,
        bool isPrivate,
        DateTimeOffset? validFrom, DateTimeOffset? validTo)
    {
        RuleType = ruleType; FlightNumber = flightNumber;
        FareBasisCode = fareBasisCode; FareFamily = fareFamily;
        CabinCode = cabinCode; BookingClass = bookingClass; CurrencyCode = currencyCode;
        MinAmount = minAmount; MaxAmount = maxAmount;
        MinPoints = minPoints; MaxPoints = maxPoints; PointsTaxes = pointsTaxes;
        TaxLines = taxLines;
        IsRefundable = isRefundable; IsChangeable = isChangeable;
        ChangeFeeAmount = changeFeeAmount; CancellationFeeAmount = cancellationFeeAmount;
        IsPrivate = isPrivate;
        ValidFrom = validFrom; ValidTo = validTo;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sums all tax line amounts from the TaxLines JSON array.
    /// Returns 0 when TaxLines is null or unparseable.
    /// </summary>
    public decimal GetTotalTaxAmount()
    {
        if (string.IsNullOrEmpty(TaxLines)) return 0m;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(TaxLines);
            return doc.RootElement.EnumerateArray()
                .Sum(e => e.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m);
        }
        catch { return 0m; }
    }
}
