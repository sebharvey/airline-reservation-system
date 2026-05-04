BEGIN TRANSACTION;

-- 1. Clear the manifest
DELETE FROM [delivery].[Manifest];

-- 2. Re-seed — one row per passenger × per FLIGHT segment, driven by order.Order
INSERT INTO [delivery].[Manifest]
    (TicketId, OrderId, InventoryId, FlightNumber, Origin, Destination,
     DepartureDate, AircraftType, SeatNumber, CabinCode,
     BookingReference, ETicketNumber, PassengerId, SegmentId,
     GivenName, Surname, DepartureTime, ArrivalTime,
     BookingType, SsrCodes, Gender, DateOfBirth, PtcCode)
SELECT
    t.TicketId,
    o.OrderId,
    TRY_CAST(seg.InventoryId AS UNIQUEIDENTIFIER)                    AS InventoryId,
    seg.FlightNumber,
    seg.Origin,
    seg.Destination,
    seg.DepartureDate,
    seg.AircraftType,
    NULLIF(c.Seat, '')                                               AS SeatNumber,
    seg.CabinCode,
    o.BookingReference,
    '932-' + CAST(t.TicketNumber AS VARCHAR(20))                     AS ETicketNumber,
    CASE
        WHEN pax.PassengerId LIKE 'PAX-%'
        THEN TRY_CAST(SUBSTRING(pax.PassengerId, 5, 20) AS INT)
        ELSE 0
    END                                                              AS PassengerId,
    c.CouponNumber                                                   AS SegmentId,
    pax.GivenName,
    pax.Surname,
    CAST(seg.DepartureTime AS TIME)                                  AS DepartureTime,
    CAST(seg.ArrivalTime   AS TIME)                                  AS ArrivalTime,
    'Confirmed'                                                      AS BookingType,
    (
        SELECT '[' + STRING_AGG('"' + JSON_VALUE(si.[value], '$.ssrCode') + '"', ',') + ']'
        FROM OPENJSON(o.OrderData, '$.orderItems') AS si
        WHERE JSON_VALUE(si.[value], '$.productType') = 'SERVICE'
          AND JSON_VALUE(si.[value], '$.passengerRef') = pax.PassengerId
          AND JSON_VALUE(si.[value], '$.segmentRef')   = seg.InventoryId
    )                                                                AS SsrCodes,
    pax.Gender,
    TRY_CAST(pax.Dob AS DATE)                                       AS DateOfBirth,
    pax.PtcCode
FROM [order].[Order] o
CROSS APPLY OPENJSON(JSON_QUERY(o.OrderData, '$.dataLists.passengers'))
    WITH (
        PassengerId VARCHAR(20)  '$.passengerId',
        GivenName   VARCHAR(100) '$.givenName',
        Surname     VARCHAR(100) '$.surname',
        Dob         VARCHAR(10)  '$.dob',
        Gender      CHAR(1)      '$.gender',
        PtcCode     VARCHAR(10)  '$.type'
    ) AS pax
CROSS APPLY OPENJSON(JSON_QUERY(o.OrderData, '$.orderItems'))
    WITH (
        ProductType   VARCHAR(20) '$.productType',
        InventoryId   VARCHAR(36) '$.inventoryId',
        FlightNumber  VARCHAR(10) '$.flightNumber',
        CabinCode     CHAR(1)     '$.cabinCode',
        DepartureDate DATE        '$.departureDate',
        DepartureTime VARCHAR(5)  '$.departureTime',
        ArrivalTime   VARCHAR(5)  '$.arrivalTime',
        Origin        CHAR(3)     '$.origin',
        Destination   CHAR(3)     '$.destination',
        AircraftType  VARCHAR(4)  '$.aircraftType'
    ) AS seg
JOIN [delivery].[Ticket] t
    ON  t.BookingReference = o.BookingReference
    AND t.PassengerId      = pax.PassengerId
    AND t.IsVoided         = 0
CROSS APPLY OPENJSON(JSON_QUERY(t.TicketData, '$.coupons'))
    WITH (
        CouponNumber  INT         '$.couponNumber',
        FlightNumber  VARCHAR(10) '$.marketing.flightNumber',
        DepartureDate DATE        '$.departureDate',
        Seat          VARCHAR(5)  '$.seat'
    ) AS c
WHERE o.OrderStatus   = 'Confirmed'
  AND seg.ProductType = 'FLIGHT'
  AND seg.InventoryId IS NOT NULL
  AND c.FlightNumber  = seg.FlightNumber
  AND c.DepartureDate = seg.DepartureDate;

COMMIT TRANSACTION;
