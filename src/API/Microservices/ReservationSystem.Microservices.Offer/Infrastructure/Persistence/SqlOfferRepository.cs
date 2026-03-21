using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;

namespace ReservationSystem.Microservices.Offer.Infrastructure.Persistence;

/// <summary>
/// SQL Server implementation of <see cref="IOfferRepository"/> using Dapper.
/// Handles DateOnly/TimeOnly conversions since Dapper does not natively support them.
/// DateOnly is stored as SQL DATE (read back as DateTime), TimeOnly is stored as SQL TIME (read back as TimeSpan).
/// </summary>
public sealed class SqlOfferRepository : IOfferRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqlOfferRepository> _logger;

    public SqlOfferRepository(
        SqlConnectionFactory connectionFactory,
        IOptions<DatabaseOptions> options,
        ILogger<SqlOfferRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // FlightInventory
    // -------------------------------------------------------------------------

    public async Task<FlightInventory?> GetInventoryByIdAsync(Guid inventoryId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                   ArrivalDayOffset, Origin, Destination, AircraftType, CabinCode,
                   TotalSeats, SeatsAvailable, SeatsSold, SeatsHeld, Status, CreatedAt, UpdatedAt
            FROM   [offer].[FlightInventory]
            WHERE  InventoryId = @InventoryId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { InventoryId = inventoryId }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToInventory(row);
    }

    public async Task<FlightInventory?> GetInventoryAsync(
        string flightNumber, DateOnly departureDate, string cabinCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                   ArrivalDayOffset, Origin, Destination, AircraftType, CabinCode,
                   TotalSeats, SeatsAvailable, SeatsSold, SeatsHeld, Status, CreatedAt, UpdatedAt
            FROM   [offer].[FlightInventory]
            WHERE  FlightNumber = @FlightNumber
              AND  DepartureDate = @DepartureDate
              AND  CabinCode = @CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                FlightNumber = flightNumber,
                DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue),
                CabinCode = cabinCode
            }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToInventory(row);
    }

    public async Task<IReadOnlyList<FlightInventory>> SearchInventoryAsync(
        string origin, string destination, DateOnly departureDate, string cabinCode, int paxCount,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                   ArrivalDayOffset, Origin, Destination, AircraftType, CabinCode,
                   TotalSeats, SeatsAvailable, SeatsSold, SeatsHeld, Status, CreatedAt, UpdatedAt
            FROM   [offer].[FlightInventory]
            WHERE  Origin = @Origin
              AND  Destination = @Destination
              AND  DepartureDate = @DepartureDate
              AND  CabinCode = @CabinCode
              AND  SeatsAvailable >= @PaxCount
              AND  Status = 'Active'
            ORDER BY DepartureTime;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                Origin = origin,
                Destination = destination,
                DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue),
                CabinCode = cabinCode,
                PaxCount = paxCount
            }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToInventory).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<FlightInventory>> GetInventoriesByFlightAsync(
        string flightNumber, DateOnly departureDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                   ArrivalDayOffset, Origin, Destination, AircraftType, CabinCode,
                   TotalSeats, SeatsAvailable, SeatsSold, SeatsHeld, Status, CreatedAt, UpdatedAt
            FROM   [offer].[FlightInventory]
            WHERE  FlightNumber = @FlightNumber
              AND  DepartureDate = @DepartureDate
            ORDER BY CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                FlightNumber = flightNumber,
                DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue)
            }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToInventory).ToList().AsReadOnly();
    }

    public async Task CreateInventoryAsync(FlightInventory inventory, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[FlightInventory]
                   (InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                    ArrivalDayOffset, Origin, Destination, AircraftType, CabinCode,
                    TotalSeats, SeatsAvailable, SeatsSold, SeatsHeld, Status, CreatedAt, UpdatedAt)
            VALUES (@InventoryId, @FlightNumber, @DepartureDate, @DepartureTime, @ArrivalTime,
                    @ArrivalDayOffset, @Origin, @Destination, @AircraftType, @CabinCode,
                    @TotalSeats, @SeatsAvailable, @SeatsSold, @SeatsHeld, @Status, @CreatedAt, @UpdatedAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapInventoryToParameters(inventory), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted FlightInventory {InventoryId} into [offer].[FlightInventory]", inventory.InventoryId);
    }

    public async Task UpdateInventoryAsync(FlightInventory inventory, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [offer].[FlightInventory]
            SET    FlightNumber     = @FlightNumber,
                   DepartureDate    = @DepartureDate,
                   DepartureTime    = @DepartureTime,
                   ArrivalTime      = @ArrivalTime,
                   ArrivalDayOffset = @ArrivalDayOffset,
                   Origin           = @Origin,
                   Destination      = @Destination,
                   AircraftType     = @AircraftType,
                   CabinCode        = @CabinCode,
                   TotalSeats       = @TotalSeats,
                   SeatsAvailable   = @SeatsAvailable,
                   SeatsSold        = @SeatsSold,
                   SeatsHeld        = @SeatsHeld,
                   Status           = @Status,
                   UpdatedAt        = @UpdatedAt
            WHERE  InventoryId = @InventoryId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, MapInventoryToParameters(inventory), commandTimeout: _options.CommandTimeoutSeconds));

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateInventoryAsync found no row for FlightInventory {InventoryId}", inventory.InventoryId);
    }

    // -------------------------------------------------------------------------
    // Fare
    // -------------------------------------------------------------------------

    public async Task<Fare?> GetFareByIdAsync(Guid fareId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, BaseFareAmount, TaxAmount, TotalAmount,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   PointsPrice, PointsTaxes, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[Fare]
            WHERE  FareId = @FareId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { FareId = fareId }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToFare(row);
    }

    public async Task<Fare?> GetFareAsync(Guid inventoryId, string fareBasisCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, BaseFareAmount, TaxAmount, TotalAmount,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   PointsPrice, PointsTaxes, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[Fare]
            WHERE  InventoryId = @InventoryId
              AND  FareBasisCode = @FareBasisCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { InventoryId = inventoryId, FareBasisCode = fareBasisCode },
                commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToFare(row);
    }

    public async Task<IReadOnlyList<Fare>> GetFaresByInventoryAsync(Guid inventoryId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, BaseFareAmount, TaxAmount, TotalAmount,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   PointsPrice, PointsTaxes, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[Fare]
            WHERE  InventoryId = @InventoryId
            ORDER BY TotalAmount;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { InventoryId = inventoryId }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToFare).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<Fare>> GetActiveFaresByInventoryAsync(Guid inventoryId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, BaseFareAmount, TaxAmount, TotalAmount,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   PointsPrice, PointsTaxes, ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[Fare]
            WHERE  InventoryId = @InventoryId
              AND  ValidFrom <= @Now
              AND  ValidTo >= @Now
            ORDER BY TotalAmount;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { InventoryId = inventoryId, Now = DateTime.UtcNow },
                commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToFare).ToList().AsReadOnly();
    }

    public async Task CreateFareAsync(Fare fare, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[Fare]
                   (FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
                    CurrencyCode, BaseFareAmount, TaxAmount, TotalAmount,
                    IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                    PointsPrice, PointsTaxes, ValidFrom, ValidTo, CreatedAt, UpdatedAt)
            VALUES (@FareId, @InventoryId, @FareBasisCode, @FareFamily, @CabinCode, @BookingClass,
                    @CurrencyCode, @BaseFareAmount, @TaxAmount, @TotalAmount,
                    @IsRefundable, @IsChangeable, @ChangeFeeAmount, @CancellationFeeAmount,
                    @PointsPrice, @PointsTaxes, @ValidFrom, @ValidTo, @CreatedAt, @UpdatedAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapFareToParameters(fare), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted Fare {FareId} into [offer].[Fare]", fare.FareId);
    }

    // -------------------------------------------------------------------------
    // StoredOffer
    // -------------------------------------------------------------------------

    public async Task<StoredOffer?> GetStoredOfferAsync(Guid offerId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT OfferId, InventoryId, FareId, FlightNumber, DepartureDate, DepartureTime,
                   ArrivalTime, ArrivalDayOffset, Origin, Destination, AircraftType, CabinCode,
                   FareBasisCode, FareFamily, BookingClass, CurrencyCode,
                   BaseFareAmount, TaxAmount, TotalAmount,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   PointsPrice, PointsTaxes, BookingType, CreatedAt, ExpiresAt, IsConsumed, UpdatedAt
            FROM   [offer].[StoredOffer]
            WHERE  OfferId = @OfferId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { OfferId = offerId }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToStoredOffer(row);
    }

    public async Task CreateStoredOfferAsync(StoredOffer offer, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[StoredOffer]
                   (OfferId, InventoryId, FareId, FlightNumber, DepartureDate, DepartureTime,
                    ArrivalTime, ArrivalDayOffset, Origin, Destination, AircraftType, CabinCode,
                    FareBasisCode, FareFamily, BookingClass, CurrencyCode,
                    BaseFareAmount, TaxAmount, TotalAmount,
                    IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                    PointsPrice, PointsTaxes, BookingType, CreatedAt, ExpiresAt, IsConsumed, UpdatedAt)
            VALUES (@OfferId, @InventoryId, @FareId, @FlightNumber, @DepartureDate, @DepartureTime,
                    @ArrivalTime, @ArrivalDayOffset, @Origin, @Destination, @AircraftType, @CabinCode,
                    @FareBasisCode, @FareFamily, @BookingClass, @CurrencyCode,
                    @BaseFareAmount, @TaxAmount, @TotalAmount,
                    @IsRefundable, @IsChangeable, @ChangeFeeAmount, @CancellationFeeAmount,
                    @PointsPrice, @PointsTaxes, @BookingType, @CreatedAt, @ExpiresAt, @IsConsumed, @UpdatedAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapStoredOfferToParameters(offer), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted StoredOffer {OfferId} into [offer].[StoredOffer]", offer.OfferId);
    }

    public async Task UpdateStoredOfferAsync(StoredOffer offer, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [offer].[StoredOffer]
            SET    IsConsumed = @IsConsumed,
                   UpdatedAt  = @UpdatedAt
            WHERE  OfferId = @OfferId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                offer.OfferId,
                offer.IsConsumed,
                offer.UpdatedAt
            }, commandTimeout: _options.CommandTimeoutSeconds));

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateStoredOfferAsync found no row for StoredOffer {OfferId}", offer.OfferId);
    }

    // -------------------------------------------------------------------------
    // InventoryHold
    // -------------------------------------------------------------------------

    public async Task<bool> HoldExistsAsync(Guid inventoryId, Guid basketId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM   [offer].[InventoryHold]
            WHERE  InventoryId = @InventoryId
              AND  BasketId = @BasketId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { InventoryId = inventoryId, BasketId = basketId },
                commandTimeout: _options.CommandTimeoutSeconds));

        return count > 0;
    }

    public async Task CreateHoldAsync(Guid inventoryId, Guid basketId, int paxCount, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[InventoryHold]
                   (HoldId, InventoryId, BasketId, PaxCount, CreatedAt)
            VALUES (@HoldId, @InventoryId, @BasketId, @PaxCount, @CreatedAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                HoldId = Guid.NewGuid(),
                InventoryId = inventoryId,
                BasketId = basketId,
                PaxCount = paxCount,
                CreatedAt = DateTime.UtcNow
            }, commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted InventoryHold for InventoryId {InventoryId}, BasketId {BasketId}", inventoryId, basketId);
    }

    // -------------------------------------------------------------------------
    // SeatReservation
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<(string SeatNumber, string Status, Guid BasketId)>> GetSeatReservationsAsync(
        Guid inventoryId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT SeatNumber, Status, BasketId
            FROM   [offer].[SeatReservation]
            WHERE  InventoryId = @InventoryId
            ORDER BY SeatNumber;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { InventoryId = inventoryId }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows
            .Select(r => ((string)r.SeatNumber, (string)r.Status, (Guid)r.BasketId))
            .ToList()
            .AsReadOnly();
    }

    public async Task CreateSeatReservationsAsync(
        Guid inventoryId, Guid basketId, IEnumerable<string> seatNumbers, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[SeatReservation]
                   (SeatReservationId, InventoryId, SeatNumber, BasketId, Status, CreatedAt, UpdatedAt)
            VALUES (@SeatReservationId, @InventoryId, @SeatNumber, @BasketId, @Status, @CreatedAt, @UpdatedAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var now = DateTime.UtcNow;
        var parameters = seatNumbers.Select(seat => new
        {
            SeatReservationId = Guid.NewGuid(),
            InventoryId = inventoryId,
            SeatNumber = seat,
            BasketId = basketId,
            Status = "Held",
            CreatedAt = now,
            UpdatedAt = now
        });

        await connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted seat reservations for InventoryId {InventoryId}, BasketId {BasketId}", inventoryId, basketId);
    }

    public async Task UpdateSeatStatusAsync(
        Guid inventoryId, string seatNumber, string status, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [offer].[SeatReservation]
            SET    Status    = @Status,
                   UpdatedAt = @UpdatedAt
            WHERE  InventoryId = @InventoryId
              AND  SeatNumber  = @SeatNumber;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                InventoryId = inventoryId,
                SeatNumber = seatNumber,
                Status = status,
                UpdatedAt = DateTime.UtcNow
            }, commandTimeout: _options.CommandTimeoutSeconds));

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateSeatStatusAsync found no row for InventoryId {InventoryId}, SeatNumber {SeatNumber}",
                inventoryId, seatNumber);
    }

    // -------------------------------------------------------------------------
    // Private mapping helpers — DateOnly/TimeOnly conversion
    // -------------------------------------------------------------------------

    private static DateOnly ToDateOnly(DateTime dt) => DateOnly.FromDateTime(dt);
    private static TimeOnly ToTimeOnly(TimeSpan ts) => TimeOnly.FromTimeSpan(ts);

    private static FlightInventory MapToInventory(dynamic row)
    {
        return FlightInventory.Reconstitute(
            inventoryId: (Guid)row.InventoryId,
            flightNumber: (string)row.FlightNumber,
            departureDate: ToDateOnly((DateTime)row.DepartureDate),
            departureTime: ToTimeOnly((TimeSpan)row.DepartureTime),
            arrivalTime: ToTimeOnly((TimeSpan)row.ArrivalTime),
            arrivalDayOffset: (int)row.ArrivalDayOffset,
            origin: (string)row.Origin,
            destination: (string)row.Destination,
            aircraftType: (string)row.AircraftType,
            cabinCode: (string)row.CabinCode,
            totalSeats: (int)row.TotalSeats,
            seatsAvailable: (int)row.SeatsAvailable,
            seatsSold: (int)row.SeatsSold,
            seatsHeld: (int)row.SeatsHeld,
            status: (string)row.Status,
            createdAt: (DateTime)row.CreatedAt,
            updatedAt: (DateTime)row.UpdatedAt);
    }

    private static Fare MapToFare(dynamic row)
    {
        return Fare.Reconstitute(
            fareId: (Guid)row.FareId,
            inventoryId: (Guid)row.InventoryId,
            fareBasisCode: (string)row.FareBasisCode,
            fareFamily: (string?)row.FareFamily,
            cabinCode: (string)row.CabinCode,
            bookingClass: (string)row.BookingClass,
            currencyCode: (string)row.CurrencyCode,
            baseFareAmount: (decimal)row.BaseFareAmount,
            taxAmount: (decimal)row.TaxAmount,
            totalAmount: (decimal)row.TotalAmount,
            isRefundable: (bool)row.IsRefundable,
            isChangeable: (bool)row.IsChangeable,
            changeFeeAmount: (decimal)row.ChangeFeeAmount,
            cancellationFeeAmount: (decimal)row.CancellationFeeAmount,
            pointsPrice: (int?)row.PointsPrice,
            pointsTaxes: (decimal?)row.PointsTaxes,
            validFrom: (DateTime)row.ValidFrom,
            validTo: (DateTime)row.ValidTo,
            createdAt: (DateTime)row.CreatedAt,
            updatedAt: (DateTime)row.UpdatedAt);
    }

    private static StoredOffer MapToStoredOffer(dynamic row)
    {
        return StoredOffer.Reconstitute(
            offerId: (Guid)row.OfferId,
            inventoryId: (Guid)row.InventoryId,
            fareId: (Guid)row.FareId,
            flightNumber: (string)row.FlightNumber,
            departureDate: ToDateOnly((DateTime)row.DepartureDate),
            departureTime: ToTimeOnly((TimeSpan)row.DepartureTime),
            arrivalTime: ToTimeOnly((TimeSpan)row.ArrivalTime),
            arrivalDayOffset: (int)row.ArrivalDayOffset,
            origin: (string)row.Origin,
            destination: (string)row.Destination,
            aircraftType: (string)row.AircraftType,
            cabinCode: (string)row.CabinCode,
            fareBasisCode: (string)row.FareBasisCode,
            fareFamily: (string?)row.FareFamily,
            bookingClass: (string)row.BookingClass,
            currencyCode: (string)row.CurrencyCode,
            baseFareAmount: (decimal)row.BaseFareAmount,
            taxAmount: (decimal)row.TaxAmount,
            totalAmount: (decimal)row.TotalAmount,
            isRefundable: (bool)row.IsRefundable,
            isChangeable: (bool)row.IsChangeable,
            changeFeeAmount: (decimal)row.ChangeFeeAmount,
            cancellationFeeAmount: (decimal)row.CancellationFeeAmount,
            pointsPrice: (int?)row.PointsPrice,
            pointsTaxes: (decimal?)row.PointsTaxes,
            seatsAvailable: (int)row.SeatsAvailable,
            bookingType: (string)row.BookingType,
            createdAt: (DateTime)row.CreatedAt,
            expiresAt: (DateTime)row.ExpiresAt,
            isConsumed: (bool)row.IsConsumed,
            updatedAt: (DateTime)row.UpdatedAt);
    }

    private static object MapInventoryToParameters(FlightInventory inv)
    {
        return new
        {
            inv.InventoryId,
            inv.FlightNumber,
            DepartureDate = inv.DepartureDate.ToDateTime(TimeOnly.MinValue),
            DepartureTime = inv.DepartureTime.ToTimeSpan(),
            ArrivalTime = inv.ArrivalTime.ToTimeSpan(),
            inv.ArrivalDayOffset,
            inv.Origin,
            inv.Destination,
            inv.AircraftType,
            inv.CabinCode,
            inv.TotalSeats,
            inv.SeatsAvailable,
            inv.SeatsSold,
            inv.SeatsHeld,
            inv.Status,
            inv.CreatedAt,
            inv.UpdatedAt
        };
    }

    private static object MapFareToParameters(Fare fare)
    {
        return new
        {
            fare.FareId,
            fare.InventoryId,
            fare.FareBasisCode,
            fare.FareFamily,
            fare.CabinCode,
            fare.BookingClass,
            fare.CurrencyCode,
            fare.BaseFareAmount,
            fare.TaxAmount,
            fare.TotalAmount,
            fare.IsRefundable,
            fare.IsChangeable,
            fare.ChangeFeeAmount,
            fare.CancellationFeeAmount,
            fare.PointsPrice,
            fare.PointsTaxes,
            fare.ValidFrom,
            fare.ValidTo,
            fare.CreatedAt,
            fare.UpdatedAt
        };
    }

    private static object MapStoredOfferToParameters(StoredOffer offer)
    {
        return new
        {
            offer.OfferId,
            offer.InventoryId,
            offer.FareId,
            offer.FlightNumber,
            DepartureDate = offer.DepartureDate.ToDateTime(TimeOnly.MinValue),
            DepartureTime = offer.DepartureTime.ToTimeSpan(),
            ArrivalTime = offer.ArrivalTime.ToTimeSpan(),
            offer.ArrivalDayOffset,
            offer.Origin,
            offer.Destination,
            offer.AircraftType,
            offer.CabinCode,
            offer.FareBasisCode,
            offer.FareFamily,
            offer.BookingClass,
            offer.CurrencyCode,
            offer.BaseFareAmount,
            offer.TaxAmount,
            offer.TotalAmount,
            offer.IsRefundable,
            offer.IsChangeable,
            offer.ChangeFeeAmount,
            offer.CancellationFeeAmount,
            offer.PointsPrice,
            offer.PointsTaxes,
            offer.BookingType,
            offer.CreatedAt,
            offer.ExpiresAt,
            offer.IsConsumed,
            offer.UpdatedAt
        };
    }
}
