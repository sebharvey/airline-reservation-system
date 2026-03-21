-- =============================================================================
-- Schema: offer
-- Naming convention: [domain].[Table] — each API owns its own schema.
-- =============================================================================

CREATE SCHEMA [offer];
GO

-- =============================================================================
-- Table: offer.FlightInventory
-- One row per flight per cabin per operating date.
-- =============================================================================

CREATE TABLE [offer].[FlightInventory]
(
    [InventoryId]      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_FlightInventory_Id]        DEFAULT NEWID(),
    [FlightNumber]     VARCHAR(10)      NOT NULL,
    [DepartureDate]    DATE             NOT NULL,
    [DepartureTime]    TIME             NOT NULL,
    [ArrivalTime]      TIME             NOT NULL,
    [ArrivalDayOffset] TINYINT          NOT NULL CONSTRAINT [DF_FlightInventory_ArrivalDayOffset] DEFAULT 0,
    [Origin]           CHAR(3)          NOT NULL,
    [Destination]      CHAR(3)          NOT NULL,
    [AircraftType]     VARCHAR(4)       NOT NULL,
    [CabinCode]        CHAR(1)          NOT NULL,
    [TotalSeats]       SMALLINT         NOT NULL,
    [SeatsAvailable]   SMALLINT         NOT NULL,
    [SeatsSold]        SMALLINT         NOT NULL CONSTRAINT [DF_FlightInventory_SeatsSold]  DEFAULT 0,
    [SeatsHeld]        SMALLINT         NOT NULL CONSTRAINT [DF_FlightInventory_SeatsHeld]  DEFAULT 0,
    [Status]           VARCHAR(20)      NOT NULL CONSTRAINT [DF_FlightInventory_Status]     DEFAULT 'Active',
    [CreatedAt]        DATETIME2        NOT NULL CONSTRAINT [DF_FlightInventory_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]        DATETIME2        NOT NULL CONSTRAINT [DF_FlightInventory_UpdatedAt]  DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_FlightInventory] PRIMARY KEY CLUSTERED ([InventoryId] ASC),
    CONSTRAINT [CHK_FlightInventory_CabinCode] CHECK ([CabinCode] IN ('F', 'J', 'W', 'Y')),
    CONSTRAINT [CHK_FlightInventory_Status]    CHECK ([Status] IN ('Active', 'Cancelled'))
);
GO

CREATE NONCLUSTERED INDEX [IX_FlightInventory_Flight]
    ON [offer].[FlightInventory] ([FlightNumber] ASC, [DepartureDate] ASC, [CabinCode] ASC)
    WHERE [Status] = 'Active';
GO

CREATE OR ALTER TRIGGER [offer].[TR_FlightInventory_UpdatedAt]
ON [offer].[FlightInventory]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF @@ROWCOUNT = 0 RETURN;
    UPDATE [offer].[FlightInventory]
    SET    [UpdatedAt] = SYSUTCDATETIME()
    FROM   [offer].[FlightInventory] o
    INNER JOIN inserted i ON o.[InventoryId] = i.[InventoryId];
END;
GO

-- =============================================================================
-- Table: offer.Fare
-- One row per fare basis per inventory record.
-- =============================================================================

CREATE TABLE [offer].[Fare]
(
    [FareId]               UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Fare_Id]        DEFAULT NEWID(),
    [InventoryId]          UNIQUEIDENTIFIER NOT NULL,
    [FareBasisCode]        VARCHAR(20)      NOT NULL,
    [FareFamily]           VARCHAR(50)      NULL,
    [CabinCode]            CHAR(1)          NOT NULL,
    [BookingClass]         CHAR(1)          NOT NULL,
    [CurrencyCode]         CHAR(3)          NOT NULL CONSTRAINT [DF_Fare_Currency]  DEFAULT 'GBP',
    [BaseFareAmount]       DECIMAL(10,2)    NOT NULL,
    [TaxAmount]            DECIMAL(10,2)    NOT NULL,
    [TotalAmount]          DECIMAL(10,2)    NOT NULL,
    [IsRefundable]         BIT              NOT NULL CONSTRAINT [DF_Fare_Refundable] DEFAULT 0,
    [IsChangeable]         BIT              NOT NULL CONSTRAINT [DF_Fare_Changeable] DEFAULT 0,
    [ChangeFeeAmount]      DECIMAL(10,2)    NOT NULL CONSTRAINT [DF_Fare_ChangeFee] DEFAULT 0.00,
    [CancellationFeeAmount] DECIMAL(10,2)   NOT NULL CONSTRAINT [DF_Fare_CancelFee] DEFAULT 0.00,
    [PointsPrice]          INT              NULL,
    [PointsTaxes]          DECIMAL(10,2)    NULL,
    [ValidFrom]            DATETIME2        NOT NULL,
    [ValidTo]              DATETIME2        NOT NULL,
    [CreatedAt]            DATETIME2        NOT NULL CONSTRAINT [DF_Fare_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]            DATETIME2        NOT NULL CONSTRAINT [DF_Fare_UpdatedAt]  DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_Fare] PRIMARY KEY CLUSTERED ([FareId] ASC),
    CONSTRAINT [FK_Fare_FlightInventory] FOREIGN KEY ([InventoryId]) REFERENCES [offer].[FlightInventory]([InventoryId]),
    CONSTRAINT [CHK_Fare_CabinCode] CHECK ([CabinCode] IN ('F', 'J', 'W', 'Y'))
);
GO

CREATE OR ALTER TRIGGER [offer].[TR_Fare_UpdatedAt]
ON [offer].[Fare]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF @@ROWCOUNT = 0 RETURN;
    UPDATE [offer].[Fare]
    SET    [UpdatedAt] = SYSUTCDATETIME()
    FROM   [offer].[Fare] f
    INNER JOIN inserted i ON f.[FareId] = i.[FareId];
END;
GO

-- =============================================================================
-- Table: offer.StoredOffer
-- One row per offer presented to a customer at search time.
-- =============================================================================

CREATE TABLE [offer].[StoredOffer]
(
    [OfferId]              UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_StoredOffer_Id]        DEFAULT NEWID(),
    [InventoryId]          UNIQUEIDENTIFIER NOT NULL,
    [FareId]               UNIQUEIDENTIFIER NOT NULL,
    [FlightNumber]         VARCHAR(10)      NOT NULL,
    [DepartureDate]        DATE             NOT NULL,
    [DepartureTime]        TIME             NOT NULL,
    [ArrivalTime]          TIME             NOT NULL,
    [ArrivalDayOffset]     TINYINT          NOT NULL CONSTRAINT [DF_StoredOffer_ArrivalDayOffset] DEFAULT 0,
    [Origin]               CHAR(3)          NOT NULL,
    [Destination]          CHAR(3)          NOT NULL,
    [AircraftType]         VARCHAR(4)       NOT NULL,
    [CabinCode]            CHAR(1)          NOT NULL,
    [FareBasisCode]        VARCHAR(20)      NOT NULL,
    [FareFamily]           VARCHAR(50)      NULL,
    [BookingClass]         CHAR(1)          NOT NULL,
    [CurrencyCode]         CHAR(3)          NOT NULL CONSTRAINT [DF_StoredOffer_Currency]  DEFAULT 'GBP',
    [BaseFareAmount]       DECIMAL(10,2)    NOT NULL,
    [TaxAmount]            DECIMAL(10,2)    NOT NULL,
    [TotalAmount]          DECIMAL(10,2)    NOT NULL,
    [IsRefundable]         BIT              NOT NULL CONSTRAINT [DF_StoredOffer_Refundable] DEFAULT 0,
    [IsChangeable]         BIT              NOT NULL CONSTRAINT [DF_StoredOffer_Changeable] DEFAULT 0,
    [ChangeFeeAmount]      DECIMAL(10,2)    NOT NULL CONSTRAINT [DF_StoredOffer_ChangeFee] DEFAULT 0.00,
    [CancellationFeeAmount] DECIMAL(10,2)   NOT NULL CONSTRAINT [DF_StoredOffer_CancelFee] DEFAULT 0.00,
    [PointsPrice]          INT              NULL,
    [PointsTaxes]          DECIMAL(10,2)    NULL,
    [BookingType]          VARCHAR(10)      NOT NULL CONSTRAINT [DF_StoredOffer_BookingType] DEFAULT 'Revenue',
    [CreatedAt]            DATETIME2        NOT NULL CONSTRAINT [DF_StoredOffer_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ExpiresAt]            DATETIME2        NOT NULL,
    [IsConsumed]           BIT              NOT NULL CONSTRAINT [DF_StoredOffer_IsConsumed] DEFAULT 0,
    [UpdatedAt]            DATETIME2        NOT NULL CONSTRAINT [DF_StoredOffer_UpdatedAt]  DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_StoredOffer] PRIMARY KEY CLUSTERED ([OfferId] ASC),
    CONSTRAINT [FK_StoredOffer_FlightInventory] FOREIGN KEY ([InventoryId]) REFERENCES [offer].[FlightInventory]([InventoryId]),
    CONSTRAINT [FK_StoredOffer_Fare] FOREIGN KEY ([FareId]) REFERENCES [offer].[Fare]([FareId])
);
GO

CREATE NONCLUSTERED INDEX [IX_StoredOffer_Expiry]
    ON [offer].[StoredOffer] ([ExpiresAt] ASC)
    WHERE [IsConsumed] = 0;
GO

CREATE OR ALTER TRIGGER [offer].[TR_StoredOffer_UpdatedAt]
ON [offer].[StoredOffer]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF @@ROWCOUNT = 0 RETURN;
    UPDATE [offer].[StoredOffer]
    SET    [UpdatedAt] = SYSUTCDATETIME()
    FROM   [offer].[StoredOffer] o
    INNER JOIN inserted i ON o.[OfferId] = i.[OfferId];
END;
GO

-- =============================================================================
-- Table: offer.SeatReservation
-- Tracks per-seat reservations for availability status.
-- =============================================================================

CREATE TABLE [offer].[SeatReservation]
(
    [SeatReservationId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_SeatReservation_Id] DEFAULT NEWID(),
    [InventoryId]       UNIQUEIDENTIFIER NOT NULL,
    [SeatNumber]        VARCHAR(5)       NOT NULL,
    [BasketId]          UNIQUEIDENTIFIER NOT NULL,
    [Status]            VARCHAR(20)      NOT NULL CONSTRAINT [DF_SeatReservation_Status] DEFAULT 'held',
    [CreatedAt]         DATETIME2        NOT NULL CONSTRAINT [DF_SeatReservation_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]         DATETIME2        NOT NULL CONSTRAINT [DF_SeatReservation_UpdatedAt] DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_SeatReservation] PRIMARY KEY CLUSTERED ([SeatReservationId] ASC),
    CONSTRAINT [FK_SeatReservation_FlightInventory] FOREIGN KEY ([InventoryId]) REFERENCES [offer].[FlightInventory]([InventoryId]),
    CONSTRAINT [UQ_SeatReservation_Seat] UNIQUE ([InventoryId], [SeatNumber]),
    CONSTRAINT [CHK_SeatReservation_Status] CHECK ([Status] IN ('held', 'sold', 'checked-in'))
);
GO

-- =============================================================================
-- Table: offer.InventoryHold
-- Tracks holds against inventory by basket for idempotency.
-- =============================================================================

CREATE TABLE [offer].[InventoryHold]
(
    [HoldId]       UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_InventoryHold_Id] DEFAULT NEWID(),
    [InventoryId]  UNIQUEIDENTIFIER NOT NULL,
    [BasketId]     UNIQUEIDENTIFIER NOT NULL,
    [PaxCount]     SMALLINT         NOT NULL,
    [CreatedAt]    DATETIME2        NOT NULL CONSTRAINT [DF_InventoryHold_CreatedAt] DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_InventoryHold] PRIMARY KEY CLUSTERED ([HoldId] ASC),
    CONSTRAINT [FK_InventoryHold_FlightInventory] FOREIGN KEY ([InventoryId]) REFERENCES [offer].[FlightInventory]([InventoryId]),
    CONSTRAINT [UQ_InventoryHold_Basket_Inventory] UNIQUE ([InventoryId], [BasketId])
);
GO
