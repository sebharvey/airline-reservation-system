using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Shared.Common.Infrastructure.Configuration;
using ReservationSystem.Shared.Common.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

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
                   ArrivalDayOffset, DepartureTimeUtc, ArrivalTimeUtc, ArrivalDayOffsetUtc,
                   Origin, Destination, AircraftType,
                   Cabins, TotalSeats, SeatsAvailable, Status, CreatedAt, UpdatedAt
            FROM   [offer].[FlightInventory]
            WHERE  InventoryId = @InventoryId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { InventoryId = inventoryId }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToInventory(row);
    }

    public async Task<FlightInventory?> GetInventoryAsync(
        string flightNumber, DateOnly departureDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                   ArrivalDayOffset, DepartureTimeUtc, ArrivalTimeUtc, ArrivalDayOffsetUtc,
                   Origin, Destination, AircraftType,
                   Cabins, TotalSeats, SeatsAvailable, Status, CreatedAt, UpdatedAt
            FROM   [offer].[FlightInventory]
            WHERE  FlightNumber = @FlightNumber
              AND  DepartureDate = @DepartureDate;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                FlightNumber = flightNumber,
                DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue)
            }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToInventory(row);
    }

    public async Task<IReadOnlyList<FlightInventory>> SearchInventoryAsync(
        string origin, string destination, DateOnly departureDate, string cabinCode, int paxCount,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT fi.InventoryId, fi.FlightNumber, fi.DepartureDate, fi.DepartureTime, fi.ArrivalTime,
                   fi.ArrivalDayOffset, fi.DepartureTimeUtc, fi.ArrivalTimeUtc, fi.ArrivalDayOffsetUtc,
                   fi.Origin, fi.Destination, fi.AircraftType,
                   fi.Cabins, fi.TotalSeats, fi.SeatsAvailable, fi.Status, fi.CreatedAt, fi.UpdatedAt
            FROM   [offer].[FlightInventory] fi
            CROSS APPLY OPENJSON(fi.Cabins) WITH (
                cabinCode CHAR(1)  '$.cabinCode',
                totalSeats INT     '$.totalSeats',
                seatsSold  INT     '$.seatsSold',
                seatsHeld  INT     '$.seatsHeld'
            ) AS c
            WHERE  fi.Origin        = @Origin
              AND  fi.Destination   = @Destination
              AND  fi.DepartureDate = @DepartureDate
              AND  fi.Status        = 'Active'
              AND  c.cabinCode      = @CabinCode
              AND  (c.totalSeats - c.seatsSold - c.seatsHeld) >= @PaxCount
            ORDER BY fi.DepartureTime;
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

    public async Task<IReadOnlyList<FlightInventory>> SearchAvailableInventoryAsync(
        string origin, string destination, DateOnly departureDate, int paxCount,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT fi.InventoryId, fi.FlightNumber, fi.DepartureDate, fi.DepartureTime, fi.ArrivalTime,
                   fi.ArrivalDayOffset, fi.DepartureTimeUtc, fi.ArrivalTimeUtc, fi.ArrivalDayOffsetUtc,
                   fi.Origin, fi.Destination, fi.AircraftType,
                   fi.Cabins, fi.TotalSeats, fi.SeatsAvailable, fi.Status, fi.CreatedAt, fi.UpdatedAt
            FROM   [offer].[FlightInventory] fi
            WHERE  fi.Origin        = @Origin
              AND  fi.Destination   = @Destination
              AND  fi.DepartureDate = @DepartureDate
              AND  fi.Status        = 'Active'
              AND  fi.SeatsAvailable >= @PaxCount
              AND  DATEADD(day, DATEDIFF(day, 0, fi.DepartureDate), CAST(fi.DepartureTime AS DATETIME)) > DATEADD(hour, 1, GETUTCDATE())
            ORDER BY fi.DepartureTime;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                Origin = origin,
                Destination = destination,
                DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue),
                PaxCount = paxCount
            }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToInventory).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<FlightInventory>> GetInventoriesByFlightAsync(
        string flightNumber, DateOnly departureDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                   ArrivalDayOffset, DepartureTimeUtc, ArrivalTimeUtc, ArrivalDayOffsetUtc,
                   Origin, Destination, AircraftType,
                   Cabins, TotalSeats, SeatsAvailable, Status, CreatedAt, UpdatedAt
            FROM   [offer].[FlightInventory]
            WHERE  FlightNumber = @FlightNumber
              AND  DepartureDate = @DepartureDate;
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

    public async Task<IReadOnlyList<FlightInventoryGroup>> GetInventoryGroupedByDateAsync(
        DateOnly departureDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                   ArrivalDayOffset, DepartureTimeUtc, ArrivalTimeUtc, ArrivalDayOffsetUtc,
                   Origin, Destination, AircraftType,
                   Cabins, TotalSeats, SeatsAvailable, Status, CreatedAt, UpdatedAt
            FROM  [offer].[FlightInventory]
            WHERE DepartureDate = @DepartureDate
            ORDER BY LEFT(FlightNumber, PATINDEX('%[0-9]%', FlightNumber) - 1),
                     CAST(SUBSTRING(FlightNumber, PATINDEX('%[0-9]%', FlightNumber), LEN(FlightNumber)) AS INT);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue) },
                commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToInventoryGroup).ToList().AsReadOnly();
    }

    public async Task CreateInventoryAsync(FlightInventory inventory, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[FlightInventory]
                   (InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                    ArrivalDayOffset, DepartureTimeUtc, ArrivalTimeUtc, ArrivalDayOffsetUtc,
                    Origin, Destination, AircraftType,
                    Cabins, TotalSeats, SeatsAvailable, Status)
            VALUES (@InventoryId, @FlightNumber, @DepartureDate, @DepartureTime, @ArrivalTime,
                    @ArrivalDayOffset, @DepartureTimeUtc, @ArrivalTimeUtc, @ArrivalDayOffsetUtc,
                    @Origin, @Destination, @AircraftType,
                    @Cabins, @TotalSeats, @SeatsAvailable, @Status);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapInventoryToInsertParameters(inventory), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted FlightInventory {InventoryId} into [offer].[FlightInventory]", inventory.InventoryId);
    }

    public async Task<IReadOnlyList<FlightInventory>> BatchCreateInventoryAsync(
        IReadOnlyList<FlightInventory> inventories, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[FlightInventory]
                   (InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime,
                    ArrivalDayOffset, DepartureTimeUtc, ArrivalTimeUtc, ArrivalDayOffsetUtc,
                    Origin, Destination, AircraftType,
                    Cabins, TotalSeats, SeatsAvailable, Status)
            SELECT @InventoryId, @FlightNumber, @DepartureDate, @DepartureTime, @ArrivalTime,
                   @ArrivalDayOffset, @DepartureTimeUtc, @ArrivalTimeUtc, @ArrivalDayOffsetUtc,
                   @Origin, @Destination, @AircraftType,
                   @Cabins, @TotalSeats, @SeatsAvailable, @Status
            WHERE NOT EXISTS (
                SELECT 1 FROM [offer].[FlightInventory]
                WHERE  FlightNumber  = @FlightNumber
                  AND  DepartureDate = @DepartureDate
            );
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var created = new List<FlightInventory>(inventories.Count);
        foreach (var inventory in inventories)
        {
            var rows = await connection.ExecuteAsync(
                new CommandDefinition(sql, MapInventoryToInsertParameters(inventory), commandTimeout: _options.CommandTimeoutSeconds));
            if (rows > 0)
                created.Add(inventory);
        }

        _logger.LogDebug("BatchCreateInventoryAsync: created {Created}, skipped {Skipped}",
            created.Count, inventories.Count - created.Count);

        return created.AsReadOnly();
    }

    public async Task UpdateInventoryAsync(FlightInventory inventory, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [offer].[FlightInventory]
            SET    Cabins          = @Cabins,
                   TotalSeats      = @TotalSeats,
                   SeatsAvailable  = @SeatsAvailable,
                   Status          = @Status
            WHERE  InventoryId = @InventoryId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                inventory.InventoryId,
                Cabins = SerializeCabins(inventory),
                inventory.TotalSeats,
                inventory.SeatsAvailable,
                inventory.Status
            }, commandTimeout: _options.CommandTimeoutSeconds));

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
                    PointsPrice, PointsTaxes, ValidFrom, ValidTo)
            VALUES (@FareId, @InventoryId, @FareBasisCode, @FareFamily, @CabinCode, @BookingClass,
                    @CurrencyCode, @BaseFareAmount, @TaxAmount, @TotalAmount,
                    @IsRefundable, @IsChangeable, @ChangeFeeAmount, @CancellationFeeAmount,
                    @PointsPrice, @PointsTaxes, @ValidFrom, @ValidTo);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapFareToParameters(fare), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted Fare {FareId} into [offer].[Fare]", fare.FareId);
    }

    public async Task BatchCreateFaresAsync(IReadOnlyList<Fare> fares, CancellationToken ct = default)
    {
        if (fares.Count == 0)
            return;

        const string sql = """
            INSERT INTO [offer].[Fare]
                   (FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
                    CurrencyCode, BaseFareAmount, TaxAmount, TotalAmount,
                    IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                    PointsPrice, PointsTaxes, ValidFrom, ValidTo)
            VALUES (@FareId, @InventoryId, @FareBasisCode, @FareFamily, @CabinCode, @BookingClass,
                    @CurrencyCode, @BaseFareAmount, @TaxAmount, @TotalAmount,
                    @IsRefundable, @IsChangeable, @ChangeFeeAmount, @CancellationFeeAmount,
                    @PointsPrice, @PointsTaxes, @ValidFrom, @ValidTo);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, fares.Select(MapFareToParameters), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("BatchCreateFaresAsync: inserted {Count} fares", fares.Count);
    }

    // -------------------------------------------------------------------------
    // StoredOffer
    // -------------------------------------------------------------------------

    public async Task<int> DeleteExpiredStoredOffersAsync(CancellationToken ct = default)
    {
        const string sql = """
            DELETE FROM [offer].[StoredOffer]
            WHERE  ExpiresAt < SYSUTCDATETIME();
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var deletedCount = await connection.ExecuteAsync(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

        return deletedCount;
    }

    public async Task<StoredOffer?> GetStoredOfferByOfferIdAsync(Guid offerId, CancellationToken ct = default)
    {
        // Locate the StoredOffer row whose FaresInfo JSON contains the given per-cabin OfferId
        // nested within $.inventories[*].offers[*].
        const string sql = """
            SELECT so.StoredOfferId, so.SessionId, so.FaresInfo, so.CreatedAt, so.ExpiresAt, so.UpdatedAt
            FROM   [offer].[StoredOffer] so
            CROSS APPLY OPENJSON(so.FaresInfo, '$.inventories') AS inv
            CROSS APPLY OPENJSON(inv.[value], '$.offers') WITH (
                offerId UNIQUEIDENTIFIER '$.offerId'
            ) AS o
            WHERE  o.offerId = @OfferId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { OfferId = offerId }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToStoredOffer(row);
    }

    public async Task<StoredOffer?> GetStoredOfferBySessionAndOfferIdAsync(Guid sessionId, Guid offerId, CancellationToken ct = default)
    {
        // Filter by the indexed SessionId first, then find the matching OfferId nested
        // within $.inventories[*].offers[*].
        const string sql = """
            SELECT so.StoredOfferId, so.SessionId, so.FaresInfo, so.CreatedAt, so.ExpiresAt, so.UpdatedAt
            FROM   [offer].[StoredOffer] so
            CROSS APPLY OPENJSON(so.FaresInfo, '$.inventories') AS inv
            CROSS APPLY OPENJSON(inv.[value], '$.offers') WITH (
                offerId UNIQUEIDENTIFIER '$.offerId'
            ) AS o
            WHERE  so.SessionId = @SessionId
              AND  o.offerId    = @OfferId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { SessionId = sessionId, OfferId = offerId }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToStoredOffer(row);
    }

    public async Task CreateStoredOfferAsync(StoredOffer offer, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[StoredOffer]
                   (StoredOfferId, SessionId, FaresInfo, ExpiresAt)
            VALUES (@StoredOfferId, @SessionId, @FaresInfo, @ExpiresAt);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new { offer.StoredOfferId, offer.SessionId, offer.FaresInfo, offer.ExpiresAt },
                commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted StoredOffer {StoredOfferId} (session {SessionId}) into [offer].[StoredOffer]",
            offer.StoredOfferId, offer.SessionId);
    }

    // -------------------------------------------------------------------------
    // InventoryHold
    // -------------------------------------------------------------------------

    public async Task<int> GetHoldCountAsync(Guid inventoryId, Guid orderId, string cabinCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM   [offer].[InventoryHold]
            WHERE  InventoryId = @InventoryId
              AND  OrderId = @OrderId
              AND  CabinCode = @CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { InventoryId = inventoryId, OrderId = orderId, CabinCode = cabinCode },
                commandTimeout: _options.CommandTimeoutSeconds));
    }

    public async Task CreateHoldAsync(Guid inventoryId, Guid orderId, string cabinCode, string? seatNumber, string? passengerId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[InventoryHold]
                   (HoldId, InventoryId, OrderId, CabinCode, SeatNumber, PassengerId, Status, HoldType, StandbyPriority)
            VALUES (@HoldId, @InventoryId, @OrderId, @CabinCode, @SeatNumber, @PassengerId, 'Held', 'Revenue', NULL);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                HoldId = Guid.NewGuid(),
                InventoryId = inventoryId,
                OrderId = orderId,
                CabinCode = cabinCode,
                SeatNumber = seatNumber,
                PassengerId = passengerId
            }, commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted InventoryHold for InventoryId {InventoryId}, OrderId {OrderId}, CabinCode {CabinCode}, SeatNumber {SeatNumber}", inventoryId, orderId, cabinCode, seatNumber);
    }

    public async Task ConfirmHoldAsync(Guid inventoryId, Guid orderId, string cabinCode, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [offer].[InventoryHold]
            SET    Status = 'Confirmed'
            WHERE  InventoryId = @InventoryId
              AND  OrderId = @OrderId
              AND  CabinCode = @CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { InventoryId = inventoryId, OrderId = orderId, CabinCode = cabinCode },
                commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Confirmed InventoryHold for InventoryId {InventoryId}, OrderId {OrderId}, CabinCode {CabinCode}", inventoryId, orderId, cabinCode);
    }

    public async Task<IReadOnlyList<InventoryHoldRecord>> GetHoldsByInventoryAsync(Guid inventoryId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT HoldId, OrderId, PassengerId, CabinCode, SeatNumber, Status, HoldType, StandbyPriority, CreatedAt
            FROM   [offer].[InventoryHold]
            WHERE  InventoryId = @InventoryId
            ORDER BY
                CASE HoldType WHEN 'Revenue' THEN 0 ELSE 1 END,
                StandbyPriority DESC,
                CreatedAt DESC;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { InventoryId = inventoryId }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(r => new InventoryHoldRecord(
            HoldId:          (Guid)r.HoldId,
            OrderId:         (Guid)r.OrderId,
            PassengerId:     (string?)r.PassengerId,
            CabinCode:       (string)r.CabinCode,
            SeatNumber:      (string?)r.SeatNumber,
            Status:          (string)r.Status,
            HoldType:        (string)r.HoldType,
            StandbyPriority: (short?)r.StandbyPriority,
            CreatedAt:       new DateTimeOffset((DateTime)r.CreatedAt, TimeSpan.Zero)))
            .ToList().AsReadOnly();
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
                   (SeatReservationId, InventoryId, SeatNumber, BasketId, Status)
            VALUES (@SeatReservationId, @InventoryId, @SeatNumber, @BasketId, @Status);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var parameters = seatNumbers.Select(seat => new
        {
            SeatReservationId = Guid.NewGuid(),
            InventoryId = inventoryId,
            SeatNumber = seat,
            BasketId = basketId,
            Status = "Held"
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
            SET    Status = @Status
            WHERE  InventoryId = @InventoryId
              AND  SeatNumber  = @SeatNumber;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                InventoryId = inventoryId,
                SeatNumber = seatNumber,
                Status = status
            }, commandTimeout: _options.CommandTimeoutSeconds));

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateSeatStatusAsync found no row for InventoryId {InventoryId}, SeatNumber {SeatNumber}",
                inventoryId, seatNumber);
    }

    // -------------------------------------------------------------------------
    // FareRule
    // -------------------------------------------------------------------------

    public async Task<FareRule?> GetFareRuleByIdAsync(Guid fareRuleId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT FareRuleId, RuleType, FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, MinAmount, MaxAmount, TaxAmount,
                   MinPoints, MaxPoints, PointsTaxes,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[FareRule]
            WHERE  FareRuleId = @FareRuleId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { FareRuleId = fareRuleId }, commandTimeout: _options.CommandTimeoutSeconds));

        return row is null ? null : MapToFareRule(row);
    }

    public async Task<IReadOnlyList<FareRule>> GetAllFareRulesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT FareRuleId, RuleType, FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, MinAmount, MaxAmount, TaxAmount,
                   MinPoints, MaxPoints, PointsTaxes,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[FareRule]
            ORDER BY FareBasisCode, CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToFareRule).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<FareRule>> SearchFareRulesAsync(string? query, CancellationToken ct = default)
    {
        const string sql = """
            SELECT FareRuleId, RuleType, FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, MinAmount, MaxAmount, TaxAmount,
                   MinPoints, MaxPoints, PointsTaxes,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[FareRule]
            WHERE  (@Query IS NULL OR @Query = ''
                    OR FareBasisCode LIKE '%' + @Query + '%'
                    OR FareFamily LIKE '%' + @Query + '%'
                    OR FlightNumber LIKE '%' + @Query + '%'
                    OR CabinCode = @Query)
            ORDER BY FareBasisCode, CabinCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { Query = query ?? string.Empty }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToFareRule).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<FareRule>> GetApplicableFareRulesAsync(
        string flightNumber, string cabinCode, DateOnly departureDate, CancellationToken ct = default)
    {
        // Returns all fare rules that apply to the given flight/cabin/date, ordered from least to
        // most specific so the caller can apply a last-wins cascade:
        //   Tier 0 — global default   (FlightNumber IS NULL,  no date bounds)
        //   Tier 1 — flight default   (FlightNumber matches,  no date bounds)
        //   Tier 2 — flight + window  (FlightNumber matches,  date within ValidFrom..ValidTo)
        const string sql = """
            SELECT FareRuleId, RuleType, FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, MinAmount, MaxAmount, TaxAmount,
                   MinPoints, MaxPoints, PointsTaxes,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[FareRule]
            WHERE  CabinCode = @CabinCode
              AND (
                  (FlightNumber IS NULL      AND ValidFrom IS NULL AND ValidTo IS NULL)
               OR (FlightNumber = @FlightNumber AND ValidFrom IS NULL AND ValidTo IS NULL)
               OR (FlightNumber = @FlightNumber AND ValidFrom <= @DepartureDate AND ValidTo > @DepartureDate)
              )
            ORDER BY
              CASE
                WHEN FlightNumber IS NULL      AND ValidFrom IS NULL THEN 0
                WHEN FlightNumber IS NOT NULL  AND ValidFrom IS NULL THEN 1
                ELSE 2
              END ASC,
              FareBasisCode ASC;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                CabinCode = cabinCode,
                FlightNumber = flightNumber,
                DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue)
            }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToFareRule).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<FareRule>> GetApplicableFareRulesForFlightsAsync(
        IReadOnlyList<string> flightNumbers, IReadOnlyList<string> cabinCodes,
        DateOnly departureDate, CancellationToken ct = default)
    {
        // Single query that retrieves all fare rules applicable to any of the supplied
        // (flightNumber, cabinCode) combinations for the given departure date, ordered
        // least-to-most-specific so callers can apply the last-wins cascade per pair.
        const string sql = """
            SELECT FareRuleId, RuleType, FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
                   CurrencyCode, MinAmount, MaxAmount, TaxAmount,
                   MinPoints, MaxPoints, PointsTaxes,
                   IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                   ValidFrom, ValidTo, CreatedAt, UpdatedAt
            FROM   [offer].[FareRule]
            WHERE  CabinCode IN @CabinCodes
              AND (
                  (FlightNumber IS NULL      AND ValidFrom IS NULL AND ValidTo IS NULL)
               OR (FlightNumber IN @FlightNumbers AND ValidFrom IS NULL AND ValidTo IS NULL)
               OR (FlightNumber IN @FlightNumbers AND ValidFrom <= @DepartureDate AND ValidTo > @DepartureDate)
              )
            ORDER BY
              CabinCode ASC,
              CASE
                WHEN FlightNumber IS NULL     AND ValidFrom IS NULL THEN 0
                WHEN FlightNumber IS NOT NULL AND ValidFrom IS NULL THEN 1
                ELSE 2
              END ASC,
              FareBasisCode ASC;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                CabinCodes    = cabinCodes,
                FlightNumbers = flightNumbers,
                DepartureDate = departureDate.ToDateTime(TimeOnly.MinValue)
            }, commandTimeout: _options.CommandTimeoutSeconds));

        return rows.Select(MapToFareRule).ToList().AsReadOnly();
    }

    public async Task CreateFareRuleAsync(FareRule fareRule, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO [offer].[FareRule]
                   (FareRuleId, RuleType, FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
                    CurrencyCode, MinAmount, MaxAmount, TaxAmount,
                    MinPoints, MaxPoints, PointsTaxes,
                    IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
                    ValidFrom, ValidTo)
            VALUES (@FareRuleId, @RuleType, @FlightNumber, @FareBasisCode, @FareFamily, @CabinCode, @BookingClass,
                    @CurrencyCode, @MinAmount, @MaxAmount, @TaxAmount,
                    @MinPoints, @MaxPoints, @PointsTaxes,
                    @IsRefundable, @IsChangeable, @ChangeFeeAmount, @CancellationFeeAmount,
                    @ValidFrom, @ValidTo);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, MapFareRuleToParameters(fareRule), commandTimeout: _options.CommandTimeoutSeconds));

        _logger.LogDebug("Inserted FareRule {FareRuleId} into [offer].[FareRule]", fareRule.FareRuleId);
    }

    public async Task UpdateFareRuleAsync(FareRule fareRule, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE [offer].[FareRule]
            SET    RuleType              = @RuleType,
                   FlightNumber          = @FlightNumber,
                   FareBasisCode         = @FareBasisCode,
                   FareFamily            = @FareFamily,
                   CabinCode             = @CabinCode,
                   BookingClass          = @BookingClass,
                   CurrencyCode          = @CurrencyCode,
                   MinAmount             = @MinAmount,
                   MaxAmount             = @MaxAmount,
                   TaxAmount             = @TaxAmount,
                   MinPoints             = @MinPoints,
                   MaxPoints             = @MaxPoints,
                   PointsTaxes           = @PointsTaxes,
                   IsRefundable          = @IsRefundable,
                   IsChangeable          = @IsChangeable,
                   ChangeFeeAmount       = @ChangeFeeAmount,
                   CancellationFeeAmount = @CancellationFeeAmount,
                   ValidFrom             = @ValidFrom,
                   ValidTo               = @ValidTo
            WHERE  FareRuleId = @FareRuleId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, MapFareRuleToParameters(fareRule), commandTimeout: _options.CommandTimeoutSeconds));

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateFareRuleAsync found no row for FareRule {FareRuleId}", fareRule.FareRuleId);
    }

    public async Task<bool> DeleteFareRuleAsync(Guid fareRuleId, CancellationToken ct = default)
    {
        const string sql = """
            DELETE FROM [offer].[FareRule]
            WHERE  FareRuleId = @FareRuleId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { FareRuleId = fareRuleId }, commandTimeout: _options.CommandTimeoutSeconds));

        return rowsAffected > 0;
    }

    // -------------------------------------------------------------------------
    // Private mapping helpers — DateOnly/TimeOnly conversion
    // -------------------------------------------------------------------------

    private static DateOnly ToDateOnly(DateTime dt) => DateOnly.FromDateTime(dt);
    private static TimeOnly ToTimeOnly(TimeSpan ts) => TimeOnly.FromTimeSpan(ts);

    private sealed record CabinJson(
        [property: JsonPropertyName("cabinCode")] string CabinCode,
        [property: JsonPropertyName("totalSeats")] int TotalSeats,
        [property: JsonPropertyName("seatsSold")]  int SeatsSold,
        [property: JsonPropertyName("seatsHeld")]  int SeatsHeld);

    private static IReadOnlyList<CabinInventory> DeserializeCabins(string json)
    {
        var list = JsonSerializer.Deserialize<List<CabinJson>>(json) ?? [];
        return list.Select(c => CabinInventory.Reconstitute(c.CabinCode, c.TotalSeats, c.SeatsSold, c.SeatsHeld))
                   .ToList().AsReadOnly();
    }

    private static string SerializeCabins(FlightInventory inv)
        => JsonSerializer.Serialize(inv.Cabins.Select(c => new CabinJson(c.CabinCode, c.TotalSeats, c.SeatsSold, c.SeatsHeld)));

    private static FlightInventoryGroup MapToInventoryGroup(dynamic row)
    {
        var cabins = JsonSerializer.Deserialize<List<CabinJson>>((string)row.Cabins) ?? [];

        FlightInventoryGroup.CabinData? MapCabin(string code)
        {
            var c = cabins.FirstOrDefault(x => x.CabinCode == code);
            return c is null ? null : new FlightInventoryGroup.CabinData
            {
                TotalSeats     = c.TotalSeats,
                SeatsAvailable = c.TotalSeats - c.SeatsSold - c.SeatsHeld,
                SeatsSold      = c.SeatsSold,
                SeatsHeld      = c.SeatsHeld
            };
        }

        return new FlightInventoryGroup
        {
            InventoryId         = (Guid)row.InventoryId,
            FlightNumber        = (string)row.FlightNumber,
            DepartureDate       = ToDateOnly((DateTime)row.DepartureDate),
            DepartureTime       = ToTimeOnly((TimeSpan)row.DepartureTime),
            ArrivalTime         = ToTimeOnly((TimeSpan)row.ArrivalTime),
            ArrivalDayOffset    = (int)row.ArrivalDayOffset,
            Origin              = (string)row.Origin,
            Destination         = (string)row.Destination,
            AircraftType        = (string)row.AircraftType,
            Status              = (string)row.Status,
            TotalSeats          = (int)row.TotalSeats,
            TotalSeatsAvailable = (int)row.SeatsAvailable,
            F = MapCabin("F"),
            J = MapCabin("J"),
            W = MapCabin("W"),
            Y = MapCabin("Y"),
        };
    }

    private static FlightInventory MapToInventory(dynamic row)
    {
        var cabins = DeserializeCabins((string)row.Cabins);
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
            cabins: cabins,
            totalSeats: (int)row.TotalSeats,
            seatsAvailable: (int)row.SeatsAvailable,
            status: (string)row.Status,
            createdAt: new DateTimeOffset((DateTime)row.CreatedAt, TimeSpan.Zero),
            updatedAt: new DateTimeOffset((DateTime)row.UpdatedAt, TimeSpan.Zero),
            departureTimeUtc: row.DepartureTimeUtc is TimeSpan dtu ? ToTimeOnly(dtu) : null,
            arrivalTimeUtc: row.ArrivalTimeUtc is TimeSpan atu ? ToTimeOnly(atu) : null,
            arrivalDayOffsetUtc: (int?)row.ArrivalDayOffsetUtc);
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
            validFrom: new DateTimeOffset((DateTime)row.ValidFrom, TimeSpan.Zero),
            validTo: new DateTimeOffset((DateTime)row.ValidTo, TimeSpan.Zero),
            createdAt: new DateTimeOffset((DateTime)row.CreatedAt, TimeSpan.Zero),
            updatedAt: new DateTimeOffset((DateTime)row.UpdatedAt, TimeSpan.Zero));
    }

    private static StoredOffer MapToStoredOffer(dynamic row)
    {
        return StoredOffer.Reconstitute(
            storedOfferId: (Guid)row.StoredOfferId,
            sessionId:     (Guid)row.SessionId,
            faresInfo:     (string)row.FaresInfo,
            createdAt:     new DateTimeOffset((DateTime)row.CreatedAt, TimeSpan.Zero),
            expiresAt:     new DateTimeOffset((DateTime)row.ExpiresAt, TimeSpan.Zero),
            updatedAt:     new DateTimeOffset((DateTime)row.UpdatedAt, TimeSpan.Zero));
    }

    private static object MapInventoryToInsertParameters(FlightInventory inv)
    {
        return new
        {
            inv.InventoryId,
            inv.FlightNumber,
            DepartureDate      = inv.DepartureDate.ToDateTime(TimeOnly.MinValue),
            DepartureTime      = inv.DepartureTime.ToTimeSpan(),
            ArrivalTime        = inv.ArrivalTime.ToTimeSpan(),
            inv.ArrivalDayOffset,
            DepartureTimeUtc   = inv.DepartureTimeUtc?.ToTimeSpan(),
            ArrivalTimeUtc     = inv.ArrivalTimeUtc?.ToTimeSpan(),
            inv.ArrivalDayOffsetUtc,
            inv.Origin,
            inv.Destination,
            inv.AircraftType,
            Cabins             = SerializeCabins(inv),
            inv.TotalSeats,
            inv.SeatsAvailable,
            inv.Status
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
            fare.ValidTo
        };
    }


    private static DateTimeOffset? ToNullableDateTimeOffset(DateTime? dt) =>
        dt.HasValue ? new DateTimeOffset(dt.Value, TimeSpan.Zero) : null;

    private static FareRule MapToFareRule(dynamic row)
    {
        return FareRule.Reconstitute(
            fareRuleId: (Guid)row.FareRuleId,
            ruleType: (string)row.RuleType,
            flightNumber: (string?)row.FlightNumber,
            fareBasisCode: (string)row.FareBasisCode,
            fareFamily: (string?)row.FareFamily,
            cabinCode: (string)row.CabinCode,
            bookingClass: (string)row.BookingClass,
            currencyCode: (string?)row.CurrencyCode,
            minAmount: (decimal?)row.MinAmount,
            maxAmount: (decimal?)row.MaxAmount,
            taxAmount: (decimal?)row.TaxAmount,
            minPoints: (int?)row.MinPoints,
            maxPoints: (int?)row.MaxPoints,
            pointsTaxes: (decimal?)row.PointsTaxes,
            isRefundable: (bool)row.IsRefundable,
            isChangeable: (bool)row.IsChangeable,
            changeFeeAmount: (decimal)row.ChangeFeeAmount,
            cancellationFeeAmount: (decimal)row.CancellationFeeAmount,
            validFrom: ToNullableDateTimeOffset((DateTime?)row.ValidFrom),
            validTo: ToNullableDateTimeOffset((DateTime?)row.ValidTo),
            createdAt: new DateTimeOffset((DateTime)row.CreatedAt, TimeSpan.Zero),
            updatedAt: new DateTimeOffset((DateTime)row.UpdatedAt, TimeSpan.Zero));
    }

    private static object MapFareRuleToParameters(FareRule fareRule)
    {
        return new
        {
            fareRule.FareRuleId,
            fareRule.RuleType,
            fareRule.FlightNumber,
            fareRule.FareBasisCode,
            fareRule.FareFamily,
            fareRule.CabinCode,
            fareRule.BookingClass,
            fareRule.CurrencyCode,
            fareRule.MinAmount,
            fareRule.MaxAmount,
            fareRule.TaxAmount,
            fareRule.MinPoints,
            fareRule.MaxPoints,
            fareRule.PointsTaxes,
            fareRule.IsRefundable,
            fareRule.IsChangeable,
            fareRule.ChangeFeeAmount,
            fareRule.CancellationFeeAmount,
            fareRule.ValidFrom,
            fareRule.ValidTo
        };
    }

    // -------------------------------------------------------------------------
    // Cleanup
    // -------------------------------------------------------------------------

    public async Task<int> DeleteExpiredFlightInventoryAsync(CancellationToken ct = default)
    {
        // Delete child rows first to satisfy FK constraints, then delete the parent
        // FlightInventory rows. All run in a single transaction.
        // A flight is considered expired when its departure datetime (DepartureDate + DepartureTime)
        // is more than 48 hours in the past.
        const string deleteHolds = """
            DELETE h
            FROM   [offer].[InventoryHold] h
            INNER JOIN [offer].[FlightInventory] i ON i.InventoryId = h.InventoryId
            WHERE  DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', i.DepartureTime), CAST(i.DepartureDate AS DATETIME2))
                   < DATEADD(HOUR, -48, SYSUTCDATETIME());
            """;

        const string deleteFares = """
            DELETE f
            FROM   [offer].[Fare] f
            INNER JOIN [offer].[FlightInventory] i ON i.InventoryId = f.InventoryId
            WHERE  DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', i.DepartureTime), CAST(i.DepartureDate AS DATETIME2))
                   < DATEADD(HOUR, -48, SYSUTCDATETIME());
            """;

        const string deleteInventory = """
            DELETE FROM [offer].[FlightInventory]
            WHERE  DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', DepartureTime), CAST(DepartureDate AS DATETIME2))
                   < DATEADD(HOUR, -48, SYSUTCDATETIME());
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var tx = connection.BeginTransaction();

        await connection.ExecuteAsync(
            new CommandDefinition(deleteHolds, transaction: tx, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

        await connection.ExecuteAsync(
            new CommandDefinition(deleteFares, transaction: tx, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

        var deletedCount = await connection.ExecuteAsync(
            new CommandDefinition(deleteInventory, transaction: tx, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

        tx.Commit();

        _logger.LogDebug("Deleted {Count} expired FlightInventory row(s) from [offer].[FlightInventory]", deletedCount);

        return deletedCount;
    }
}
