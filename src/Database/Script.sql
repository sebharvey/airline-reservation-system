-- =============================================================================
-- APEX AIR RESERVATION SYSTEM — DATABASE SCHEMA & SEED DATA
-- =============================================================================
-- Domains: offer, order (incl. SsrCatalogue), payment, delivery,
--          seat, bag, schedule, customer, identity, disruption
--
-- Conventions:
--   • All PKs use UNIQUEIDENTIFIER / NEWID()
--   • CreatedAt / UpdatedAt are database-generated (DEFAULT + AFTER UPDATE trigger)
--   • Schema-qualified table names throughout
--   • Wrapped in a single transaction — rolls back completely on any error
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- automatically roll back on any error

BEGIN TRANSACTION;

BEGIN TRY

-- =============================================================================
-- SCHEMAS
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'offer')      EXEC('CREATE SCHEMA [offer]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'order')      EXEC('CREATE SCHEMA [order]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'payment')    EXEC('CREATE SCHEMA [payment]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'delivery')   EXEC('CREATE SCHEMA [delivery]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'seat')       EXEC('CREATE SCHEMA [seat]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'bag')        EXEC('CREATE SCHEMA [bag]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'schedule')   EXEC('CREATE SCHEMA [schedule]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'customer')   EXEC('CREATE SCHEMA [customer]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'identity')   EXEC('CREATE SCHEMA [identity]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'disruption') EXEC('CREATE SCHEMA [disruption]');

PRINT 'Schemas created.';

-- =============================================================================
-- OFFER DOMAIN
-- =============================================================================

-- offer.FlightInventory -------------------------------------------------------
CREATE TABLE [offer].[FlightInventory] (
    InventoryId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FlightInventory_Id       DEFAULT NEWID(),
    FlightNumber      VARCHAR(10)      NOT NULL,
    DepartureDate     DATE             NOT NULL,
    DepartureTime     TIME             NOT NULL,
    ArrivalTime       TIME             NOT NULL,
    ArrivalDayOffset  TINYINT          NOT NULL CONSTRAINT DF_FlightInventory_Offset   DEFAULT 0,
    Origin            CHAR(3)          NOT NULL,
    Destination       CHAR(3)          NOT NULL,
    AircraftType      VARCHAR(4)       NOT NULL,
    CabinCode         CHAR(1)          NOT NULL,
    TotalSeats        SMALLINT         NOT NULL,
    SeatsAvailable    SMALLINT         NOT NULL,
    SeatsSold         SMALLINT         NOT NULL CONSTRAINT DF_FlightInventory_Sold     DEFAULT 0,
    SeatsHeld         SMALLINT         NOT NULL CONSTRAINT DF_FlightInventory_Held     DEFAULT 0,
    Status            VARCHAR(20)      NOT NULL CONSTRAINT DF_FlightInventory_Status   DEFAULT 'Active',
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_FlightInventory_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_FlightInventory_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_FlightInventory PRIMARY KEY (InventoryId),
    CONSTRAINT CHK_FlightInventory_Cabin  CHECK (CabinCode IN ('F','J','W','Y')),
    CONSTRAINT CHK_FlightInventory_Status CHECK (Status IN ('Active','Cancelled'))
);

CREATE INDEX IX_FlightInventory_Flight
    ON [offer].[FlightInventory] (FlightNumber, DepartureDate, CabinCode)
    WHERE Status = 'Active';

GO
CREATE TRIGGER [offer].[TR_FlightInventory_UpdatedAt]
ON [offer].[FlightInventory]
AFTER UPDATE AS
    UPDATE [offer].[FlightInventory]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [offer].[FlightInventory] fi
    INNER JOIN inserted i ON fi.InventoryId = i.InventoryId;
GO

-- offer.Fare ------------------------------------------------------------------
CREATE TABLE [offer].[Fare] (
    FareId                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Fare_Id       DEFAULT NEWID(),
    InventoryId            UNIQUEIDENTIFIER NOT NULL,
    FareBasisCode          VARCHAR(20)      NOT NULL,
    FareFamily             VARCHAR(50)          NULL,
    CabinCode              CHAR(1)          NOT NULL,
    BookingClass           CHAR(1)          NOT NULL,
    CurrencyCode           CHAR(3)          NOT NULL CONSTRAINT DF_Fare_Currency DEFAULT 'GBP',
    BaseFareAmount         DECIMAL(10,2)    NOT NULL,
    TaxAmount              DECIMAL(10,2)    NOT NULL,
    TotalAmount            DECIMAL(10,2)    NOT NULL,
    IsRefundable           BIT              NOT NULL CONSTRAINT DF_Fare_Refundable  DEFAULT 0,
    IsChangeable           BIT              NOT NULL CONSTRAINT DF_Fare_Changeable  DEFAULT 0,
    ChangeFeeAmount        DECIMAL(10,2)    NOT NULL CONSTRAINT DF_Fare_ChangeFee   DEFAULT 0.00,
    CancellationFeeAmount  DECIMAL(10,2)    NOT NULL CONSTRAINT DF_Fare_CancelFee   DEFAULT 0.00,
    PointsPrice            INT                  NULL,
    PointsTaxes            DECIMAL(10,2)        NULL,
    ValidFrom              DATETIME2        NOT NULL,
    ValidTo                DATETIME2        NOT NULL,
    CreatedAt              DATETIME2        NOT NULL CONSTRAINT DF_Fare_Created     DEFAULT SYSUTCDATETIME(),
    UpdatedAt              DATETIME2        NOT NULL CONSTRAINT DF_Fare_Updated     DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Fare            PRIMARY KEY (FareId),
    CONSTRAINT FK_Fare_Inventory  FOREIGN KEY (InventoryId) REFERENCES [offer].[FlightInventory](InventoryId),
    CONSTRAINT CHK_Fare_Cabin     CHECK (CabinCode IN ('F','J','W','Y'))
);

CREATE INDEX IX_Fare_Inventory ON [offer].[Fare] (InventoryId);

GO
CREATE TRIGGER [offer].[TR_Fare_UpdatedAt]
ON [offer].[Fare]
AFTER UPDATE AS
    UPDATE [offer].[Fare]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [offer].[Fare] f
    INNER JOIN inserted i ON f.FareId = i.FareId;
GO

-- offer.StoredOffer -----------------------------------------------------------
CREATE TABLE [offer].[StoredOffer] (
    OfferId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_StoredOffer_Id       DEFAULT NEWID(),
    InventoryId            UNIQUEIDENTIFIER NOT NULL,
    FareId                 UNIQUEIDENTIFIER NOT NULL,
    FlightNumber           VARCHAR(10)      NOT NULL,
    DepartureDate          DATE             NOT NULL,
    Origin                 CHAR(3)          NOT NULL,
    Destination            CHAR(3)          NOT NULL,
    FareBasisCode          VARCHAR(20)      NOT NULL,
    FareFamily             VARCHAR(50)          NULL,
    CurrencyCode           CHAR(3)          NOT NULL CONSTRAINT DF_StoredOffer_Currency DEFAULT 'GBP',
    BaseFareAmount         DECIMAL(10,2)    NOT NULL,
    TaxAmount              DECIMAL(10,2)    NOT NULL,
    TotalAmount            DECIMAL(10,2)    NOT NULL,
    IsRefundable           BIT              NOT NULL CONSTRAINT DF_StoredOffer_Refund   DEFAULT 0,
    IsChangeable           BIT              NOT NULL CONSTRAINT DF_StoredOffer_Change   DEFAULT 0,
    ChangeFeeAmount        DECIMAL(10,2)    NOT NULL CONSTRAINT DF_StoredOffer_ChangeFee DEFAULT 0.00,
    CancellationFeeAmount  DECIMAL(10,2)    NOT NULL CONSTRAINT DF_StoredOffer_CancelFee DEFAULT 0.00,
    PointsPrice            INT                  NULL,
    PointsTaxes            DECIMAL(10,2)        NULL,
    BookingType            VARCHAR(10)      NOT NULL CONSTRAINT DF_StoredOffer_BkType   DEFAULT 'Revenue',
    CreatedAt              DATETIME2        NOT NULL CONSTRAINT DF_StoredOffer_Created  DEFAULT SYSUTCDATETIME(),
    ExpiresAt              DATETIME2        NOT NULL,
    IsConsumed             BIT              NOT NULL CONSTRAINT DF_StoredOffer_Consumed DEFAULT 0,
    UpdatedAt              DATETIME2        NOT NULL CONSTRAINT DF_StoredOffer_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_StoredOffer          PRIMARY KEY (OfferId),
    CONSTRAINT FK_StoredOffer_Inventory FOREIGN KEY (InventoryId) REFERENCES [offer].[FlightInventory](InventoryId),
    CONSTRAINT FK_StoredOffer_Fare      FOREIGN KEY (FareId)      REFERENCES [offer].[Fare](FareId),
    CONSTRAINT CHK_StoredOffer_BkType   CHECK (BookingType IN ('Revenue','Reward'))
);

CREATE INDEX IX_StoredOffer_Expiry
    ON [offer].[StoredOffer] (ExpiresAt)
    WHERE IsConsumed = 0;

GO
CREATE TRIGGER [offer].[TR_StoredOffer_UpdatedAt]
ON [offer].[StoredOffer]
AFTER UPDATE AS
    UPDATE [offer].[StoredOffer]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [offer].[StoredOffer] so
    INNER JOIN inserted i ON so.OfferId = i.OfferId;
GO

-- =============================================================================
-- ORDER DOMAIN
-- =============================================================================

-- order.Basket ----------------------------------------------------------------
CREATE TABLE [order].[Basket] (
    BasketId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Basket_Id       DEFAULT NEWID(),
    ChannelCode       VARCHAR(20)      NOT NULL,
    CurrencyCode      CHAR(3)          NOT NULL CONSTRAINT DF_Basket_Currency DEFAULT 'GBP',
    BasketStatus      VARCHAR(20)      NOT NULL CONSTRAINT DF_Basket_Status   DEFAULT 'Active',
    TotalFareAmount   DECIMAL(10,2)        NULL,
    TotalSeatAmount   DECIMAL(10,2)        NULL CONSTRAINT DF_Basket_Seat     DEFAULT 0.00,
    TotalBagAmount    DECIMAL(10,2)        NULL CONSTRAINT DF_Basket_Bag      DEFAULT 0.00,
    TotalAmount       DECIMAL(10,2)        NULL,
    ExpiresAt         DATETIME2        NOT NULL,
    ConfirmedOrderId  UNIQUEIDENTIFIER     NULL,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Basket_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Basket_Updated  DEFAULT SYSUTCDATETIME(),
    Version           INT              NOT NULL CONSTRAINT DF_Basket_Version  DEFAULT 1,
    BasketData        NVARCHAR(MAX)    NOT NULL,
    CONSTRAINT PK_Basket              PRIMARY KEY (BasketId),
    CONSTRAINT CHK_Basket_Status      CHECK (BasketStatus IN ('Active','Expired','Abandoned','Confirmed')),
    CONSTRAINT CHK_Basket_Channel     CHECK (ChannelCode IN ('WEB','APP','NDC','KIOSK','CC','AIRPORT')),
    CONSTRAINT CHK_Basket_Data        CHECK (ISJSON(BasketData) = 1)
);

CREATE INDEX IX_Basket_Status_Expiry
    ON [order].[Basket] (BasketStatus, ExpiresAt)
    WHERE BasketStatus = 'Active';

GO
CREATE TRIGGER [order].[TR_Basket_UpdatedAt]
ON [order].[Basket]
AFTER UPDATE AS
    UPDATE [order].[Basket]
    SET    UpdatedAt = SYSUTCDATETIME(),
           Version   = Version + 1
    FROM   [order].[Basket] b
    INNER JOIN inserted i ON b.BasketId = i.BasketId;
GO

-- order.Order -----------------------------------------------------------------
CREATE TABLE [order].[Order] (
    OrderId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Order_Id       DEFAULT NEWID(),
    BookingReference  CHAR(6)              NULL,
    OrderStatus       VARCHAR(20)      NOT NULL CONSTRAINT DF_Order_Status   DEFAULT 'Draft',
    ChannelCode       VARCHAR(20)      NOT NULL,
    CurrencyCode      CHAR(3)          NOT NULL CONSTRAINT DF_Order_Currency DEFAULT 'GBP',
    TicketingTimeLimit DATETIME2           NULL,
    TotalAmount       DECIMAL(10,2)        NULL,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Order_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Order_Updated  DEFAULT SYSUTCDATETIME(),
    Version           INT              NOT NULL CONSTRAINT DF_Order_Version  DEFAULT 1,
    OrderData         NVARCHAR(MAX)    NOT NULL,
    CONSTRAINT PK_Order          PRIMARY KEY (OrderId),
    CONSTRAINT CHK_Order_Status  CHECK (OrderStatus IN ('OrderInit','Draft','Confirmed','Changed','Cancelled')),
    CONSTRAINT CHK_Order_Channel CHECK (ChannelCode IN ('WEB','APP','NDC','KIOSK','CC','AIRPORT')),
    CONSTRAINT CHK_Order_Data    CHECK (ISJSON(OrderData) = 1)
);

CREATE UNIQUE INDEX IX_Order_BookingReference
    ON [order].[Order] (BookingReference)
    WHERE BookingReference IS NOT NULL;

GO
CREATE TRIGGER [order].[TR_Order_UpdatedAt]
ON [order].[Order]
AFTER UPDATE AS
    UPDATE [order].[Order]
    SET    UpdatedAt = SYSUTCDATETIME(),
           Version   = Version + 1
    FROM   [order].[Order] o
    INNER JOIN inserted i ON o.OrderId = i.OrderId;
GO

-- =============================================================================
-- PAYMENT DOMAIN
-- =============================================================================

-- payment.Payment -------------------------------------------------------------
CREATE TABLE [payment].[Payment] (
    PaymentId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Payment_Id       DEFAULT NEWID(),
    PaymentReference   VARCHAR(20)      NOT NULL,
    BookingReference   CHAR(6)              NULL,
    PaymentType        VARCHAR(30)      NOT NULL,
    Method             VARCHAR(20)      NOT NULL,
    CardType           VARCHAR(20)          NULL,
    CardLast4          CHAR(4)              NULL,
    CurrencyCode       CHAR(3)          NOT NULL CONSTRAINT DF_Payment_Currency DEFAULT 'GBP',
    AuthorisedAmount   DECIMAL(10,2)    NOT NULL,
    SettledAmount      DECIMAL(10,2)        NULL,
    Status             VARCHAR(20)      NOT NULL,
    AuthorisedAt       DATETIME2        NOT NULL CONSTRAINT DF_Payment_AuthAt   DEFAULT SYSUTCDATETIME(),
    SettledAt          DATETIME2            NULL,
    Description        VARCHAR(255)         NULL,
    CreatedAt          DATETIME2        NOT NULL CONSTRAINT DF_Payment_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt          DATETIME2        NOT NULL CONSTRAINT DF_Payment_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Payment              PRIMARY KEY (PaymentId),
    CONSTRAINT UQ_Payment_Reference    UNIQUE (PaymentReference),
    CONSTRAINT CHK_Payment_Type        CHECK (PaymentType IN ('Fare','SeatAncillary','BagAncillary','FareChange','Cancellation','Refund','RewardTaxes','RewardChangeTaxes')),
    CONSTRAINT CHK_Payment_Status      CHECK (Status IN ('Authorised','Settled','PartiallySettled','Refunded','Declined','Voided'))
);

CREATE INDEX IX_Payment_BookingReference
    ON [payment].[Payment] (BookingReference)
    WHERE BookingReference IS NOT NULL;

CREATE INDEX IX_Payment_PaymentReference ON [payment].[Payment] (PaymentReference);

GO
CREATE TRIGGER [payment].[TR_Payment_UpdatedAt]
ON [payment].[Payment]
AFTER UPDATE AS
    UPDATE [payment].[Payment]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [payment].[Payment] p
    INNER JOIN inserted i ON p.PaymentId = i.PaymentId;
GO

-- payment.PaymentEvent --------------------------------------------------------
CREATE TABLE [payment].[PaymentEvent] (
    PaymentEventId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PaymentEvent_Id      DEFAULT NEWID(),
    PaymentId       UNIQUEIDENTIFIER NOT NULL,
    EventType       VARCHAR(20)      NOT NULL,
    Amount          DECIMAL(10,2)    NOT NULL,
    CurrencyCode    CHAR(3)          NOT NULL CONSTRAINT DF_PaymentEvent_Currency DEFAULT 'GBP',
    Notes           VARCHAR(255)         NULL,
    CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_PaymentEvent_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2        NOT NULL CONSTRAINT DF_PaymentEvent_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PaymentEvent        PRIMARY KEY (PaymentEventId),
    CONSTRAINT FK_PaymentEvent_Payment FOREIGN KEY (PaymentId) REFERENCES [payment].[Payment](PaymentId),
    CONSTRAINT CHK_PaymentEvent_Type  CHECK (EventType IN ('Authorised','Settled','PartialSettlement','Refunded','Declined','Voided'))
);

CREATE INDEX IX_PaymentEvent_PaymentId ON [payment].[PaymentEvent] (PaymentId);

GO
CREATE TRIGGER [payment].[TR_PaymentEvent_UpdatedAt]
ON [payment].[PaymentEvent]
AFTER UPDATE AS
    UPDATE [payment].[PaymentEvent]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [payment].[PaymentEvent] pe
    INNER JOIN inserted i ON pe.PaymentEventId = i.PaymentEventId;
GO

-- =============================================================================
-- DELIVERY DOMAIN
-- =============================================================================

-- delivery.Ticket -------------------------------------------------------------
CREATE TABLE [delivery].[Ticket] (
    TicketId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Ticket_Id       DEFAULT NEWID(),
    ETicketNumber     VARCHAR(20)      NOT NULL,
    InventoryId       UNIQUEIDENTIFIER NOT NULL,
    FlightNumber      VARCHAR(10)      NOT NULL,
    DepartureDate     DATE             NOT NULL,
    BookingReference  CHAR(6)          NOT NULL,
    PassengerId       VARCHAR(20)      NOT NULL,
    GivenName         VARCHAR(100)     NOT NULL,
    Surname           VARCHAR(100)     NOT NULL,
    CabinCode         CHAR(1)          NOT NULL,
    FareBasisCode     VARCHAR(20)      NOT NULL,
    IsVoided          BIT              NOT NULL CONSTRAINT DF_Ticket_Voided   DEFAULT 0,
    VoidedAt          DATETIME2            NULL,
    TicketData        NVARCHAR(MAX)    NOT NULL,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Ticket_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Ticket_Updated  DEFAULT SYSUTCDATETIME(),
    Version           INT              NOT NULL CONSTRAINT DF_Ticket_Version  DEFAULT 1,
    CONSTRAINT PK_Ticket          PRIMARY KEY (TicketId),
    CONSTRAINT UQ_Ticket_ETicket  UNIQUE (ETicketNumber),
    CONSTRAINT CHK_Ticket_Cabin   CHECK (CabinCode IN ('F','J','W','Y')),
    CONSTRAINT CHK_Ticket_Data    CHECK (ISJSON(TicketData) = 1)
);

CREATE INDEX IX_Ticket_BookingReference ON [delivery].[Ticket] (BookingReference);
CREATE INDEX IX_Ticket_Flight           ON [delivery].[Ticket] (FlightNumber, DepartureDate);

GO
CREATE TRIGGER [delivery].[TR_Ticket_UpdatedAt]
ON [delivery].[Ticket]
AFTER UPDATE AS
    UPDATE [delivery].[Ticket]
    SET    UpdatedAt = SYSUTCDATETIME(),
           Version   = Version + 1
    FROM   [delivery].[Ticket] t
    INNER JOIN inserted i ON t.TicketId = i.TicketId;
GO

-- delivery.Manifest -----------------------------------------------------------
CREATE TABLE [delivery].[Manifest] (
    ManifestId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Manifest_Id      DEFAULT NEWID(),
    TicketId          UNIQUEIDENTIFIER NOT NULL,
    InventoryId       UNIQUEIDENTIFIER NOT NULL,
    FlightNumber      VARCHAR(10)      NOT NULL,
    DepartureDate     DATE             NOT NULL,
    AircraftType      CHAR(4)          NOT NULL,
    SeatNumber        VARCHAR(5)       NOT NULL,
    CabinCode         CHAR(1)          NOT NULL,
    BookingReference  CHAR(6)          NOT NULL,
    ETicketNumber     VARCHAR(20)      NOT NULL,
    PassengerId       VARCHAR(20)      NOT NULL,
    GivenName         VARCHAR(100)     NOT NULL,
    Surname           VARCHAR(100)     NOT NULL,
    SsrCodes          NVARCHAR(500)        NULL,
    DepartureTime     TIME             NOT NULL,
    ArrivalTime       TIME             NOT NULL,
    CheckedIn         BIT              NOT NULL CONSTRAINT DF_Manifest_Checked DEFAULT 0,
    CheckedInAt       DATETIME2            NULL,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Manifest_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Manifest_Updated DEFAULT SYSUTCDATETIME(),
    Version           INT              NOT NULL CONSTRAINT DF_Manifest_Version DEFAULT 1,
    CONSTRAINT PK_Manifest               PRIMARY KEY (ManifestId),
    CONSTRAINT FK_Manifest_Ticket        FOREIGN KEY (TicketId) REFERENCES [delivery].[Ticket](TicketId),
    CONSTRAINT UQ_Manifest_Seat          UNIQUE (InventoryId, SeatNumber),
    CONSTRAINT UQ_Manifest_Pax           UNIQUE (InventoryId, ETicketNumber),
    CONSTRAINT CHK_Manifest_Cabin        CHECK (CabinCode IN ('F','J','W','Y'))
);

CREATE INDEX IX_Manifest_Flight          ON [delivery].[Manifest] (FlightNumber, DepartureDate);
CREATE INDEX IX_Manifest_BookingReference ON [delivery].[Manifest] (BookingReference);

GO
CREATE TRIGGER [delivery].[TR_Manifest_UpdatedAt]
ON [delivery].[Manifest]
AFTER UPDATE AS
    UPDATE [delivery].[Manifest]
    SET    UpdatedAt = SYSUTCDATETIME(),
           Version   = Version + 1
    FROM   [delivery].[Manifest] m
    INNER JOIN inserted i ON m.ManifestId = i.ManifestId;
GO

-- delivery.Document -----------------------------------------------------------
CREATE TABLE [delivery].[Document] (
    DocumentId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Document_Id       DEFAULT NEWID(),
    DocumentNumber    VARCHAR(20)      NOT NULL,
    DocumentType      VARCHAR(30)      NOT NULL,
    BookingReference  CHAR(6)          NOT NULL,
    ETicketNumber     VARCHAR(20)      NOT NULL,
    PassengerId       VARCHAR(20)      NOT NULL,
    SegmentRef        VARCHAR(20)      NOT NULL,
    PaymentReference  VARCHAR(20)      NOT NULL,
    Amount            DECIMAL(10,2)    NOT NULL,
    CurrencyCode      CHAR(3)          NOT NULL CONSTRAINT DF_Document_Currency DEFAULT 'GBP',
    IsVoided          BIT              NOT NULL CONSTRAINT DF_Document_Voided   DEFAULT 0,
    DocumentData      NVARCHAR(MAX)    NOT NULL,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Document_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Document_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Document           PRIMARY KEY (DocumentId),
    CONSTRAINT UQ_Document_Number    UNIQUE (DocumentNumber),
    CONSTRAINT CHK_Document_Type     CHECK (DocumentType IN ('SeatAncillary','BagAncillary')),
    CONSTRAINT CHK_Document_Data     CHECK (ISJSON(DocumentData) = 1)
);

CREATE INDEX IX_Document_BookingReference ON [delivery].[Document] (BookingReference);
CREATE INDEX IX_Document_ETicketNumber    ON [delivery].[Document] (ETicketNumber);

GO
CREATE TRIGGER [delivery].[TR_Document_UpdatedAt]
ON [delivery].[Document]
AFTER UPDATE AS
    UPDATE [delivery].[Document]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [delivery].[Document] d
    INNER JOIN inserted i ON d.DocumentId = i.DocumentId;
GO

-- =============================================================================
-- SEAT DOMAIN
-- =============================================================================

-- seat.AircraftType -----------------------------------------------------------
CREATE TABLE [seat].[AircraftType] (
    AircraftTypeCode  CHAR(4)          NOT NULL,
    Manufacturer      VARCHAR(50)      NOT NULL,
    FriendlyName      VARCHAR(100)         NULL,
    TotalSeats        SMALLINT         NOT NULL,
    IsActive          BIT              NOT NULL CONSTRAINT DF_AircraftType_Active  DEFAULT 1,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_AircraftType_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_AircraftType_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AircraftType PRIMARY KEY (AircraftTypeCode)
);

GO
CREATE TRIGGER [seat].[TR_AircraftType_UpdatedAt]
ON [seat].[AircraftType]
AFTER UPDATE AS
    UPDATE [seat].[AircraftType]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [seat].[AircraftType] a
    INNER JOIN inserted i ON a.AircraftTypeCode = i.AircraftTypeCode;
GO

-- seat.Seatmap ----------------------------------------------------------------
CREATE TABLE [seat].[Seatmap] (
    SeatmapId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Seatmap_Id      DEFAULT NEWID(),
    AircraftTypeCode  CHAR(4)          NOT NULL,
    Version           INT              NOT NULL CONSTRAINT DF_Seatmap_Version DEFAULT 1,
    IsActive          BIT              NOT NULL CONSTRAINT DF_Seatmap_Active  DEFAULT 1,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Seatmap_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_Seatmap_Updated DEFAULT SYSUTCDATETIME(),
    CabinLayout       NVARCHAR(MAX)    NOT NULL,
    CONSTRAINT PK_Seatmap             PRIMARY KEY (SeatmapId),
    CONSTRAINT FK_Seatmap_AircraftType FOREIGN KEY (AircraftTypeCode) REFERENCES [seat].[AircraftType](AircraftTypeCode),
    CONSTRAINT CHK_Seatmap_Layout     CHECK (ISJSON(CabinLayout) = 1)
);

CREATE INDEX IX_Seatmap_AircraftType
    ON [seat].[Seatmap] (AircraftTypeCode)
    WHERE IsActive = 1;

GO
CREATE TRIGGER [seat].[TR_Seatmap_UpdatedAt]
ON [seat].[Seatmap]
AFTER UPDATE AS
    UPDATE [seat].[Seatmap]
    SET    UpdatedAt = SYSUTCDATETIME(),
           Version   = Version + 1
    FROM   [seat].[Seatmap] s
    INNER JOIN inserted i ON s.SeatmapId = i.SeatmapId;
GO

-- seat.SeatPricing ------------------------------------------------------------
CREATE TABLE [seat].[SeatPricing] (
    SeatPricingId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SeatPricing_Id      DEFAULT NEWID(),
    CabinCode      CHAR(1)          NOT NULL,
    SeatPosition   VARCHAR(10)      NOT NULL,
    CurrencyCode   CHAR(3)          NOT NULL CONSTRAINT DF_SeatPricing_Currency DEFAULT 'GBP',
    Price          DECIMAL(10,2)    NOT NULL,
    IsActive       BIT              NOT NULL CONSTRAINT DF_SeatPricing_Active   DEFAULT 1,
    ValidFrom      DATETIME2        NOT NULL,
    ValidTo        DATETIME2            NULL,
    CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_SeatPricing_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2        NOT NULL CONSTRAINT DF_SeatPricing_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_SeatPricing              PRIMARY KEY (SeatPricingId),
    CONSTRAINT UQ_SeatPricing_CabinPos     UNIQUE (CabinCode, SeatPosition, CurrencyCode),
    CONSTRAINT CHK_SeatPricing_Cabin       CHECK (CabinCode IN ('W','Y')),
    CONSTRAINT CHK_SeatPricing_Position    CHECK (SeatPosition IN ('Window','Aisle','Middle'))
);

GO
CREATE TRIGGER [seat].[TR_SeatPricing_UpdatedAt]
ON [seat].[SeatPricing]
AFTER UPDATE AS
    UPDATE [seat].[SeatPricing]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [seat].[SeatPricing] sp
    INNER JOIN inserted i ON sp.SeatPricingId = i.SeatPricingId;
GO

-- =============================================================================
-- BAG DOMAIN
-- =============================================================================

-- bag.BagPolicy ---------------------------------------------------------------
CREATE TABLE [bag].[BagPolicy] (
    PolicyId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BagPolicy_Id      DEFAULT NEWID(),
    CabinCode         CHAR(1)          NOT NULL,
    FreeBagsIncluded  TINYINT          NOT NULL,
    MaxWeightKgPerBag TINYINT          NOT NULL,
    IsActive          BIT              NOT NULL CONSTRAINT DF_BagPolicy_Active   DEFAULT 1,
    CreatedAt         DATETIME2        NOT NULL CONSTRAINT DF_BagPolicy_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2        NOT NULL CONSTRAINT DF_BagPolicy_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BagPolicy      PRIMARY KEY (PolicyId),
    CONSTRAINT UQ_BagPolicy_Cabin UNIQUE (CabinCode),
    CONSTRAINT CHK_BagPolicy_Cabin CHECK (CabinCode IN ('F','J','W','Y'))
);

GO
CREATE TRIGGER [bag].[TR_BagPolicy_UpdatedAt]
ON [bag].[BagPolicy]
AFTER UPDATE AS
    UPDATE [bag].[BagPolicy]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [bag].[BagPolicy] bp
    INNER JOIN inserted i ON bp.PolicyId = i.PolicyId;
GO

-- bag.BagPricing --------------------------------------------------------------
CREATE TABLE [bag].[BagPricing] (
    PricingId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BagPricing_Id       DEFAULT NEWID(),
    BagSequence   TINYINT          NOT NULL,
    CurrencyCode  CHAR(3)          NOT NULL CONSTRAINT DF_BagPricing_Currency DEFAULT 'GBP',
    Price         DECIMAL(10,2)    NOT NULL,
    IsActive      BIT              NOT NULL CONSTRAINT DF_BagPricing_Active   DEFAULT 1,
    ValidFrom     DATETIME2        NOT NULL,
    ValidTo       DATETIME2            NULL,
    CreatedAt     DATETIME2        NOT NULL CONSTRAINT DF_BagPricing_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2        NOT NULL CONSTRAINT DF_BagPricing_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BagPricing          PRIMARY KEY (PricingId),
    CONSTRAINT UQ_BagPricing_Sequence UNIQUE (BagSequence, CurrencyCode)
);

GO
CREATE TRIGGER [bag].[TR_BagPricing_UpdatedAt]
ON [bag].[BagPricing]
AFTER UPDATE AS
    UPDATE [bag].[BagPricing]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [bag].[BagPricing] bp
    INNER JOIN inserted i ON bp.PricingId = i.PricingId;
GO

-- =============================================================================
-- SCHEDULE DOMAIN
-- =============================================================================

-- schedule.FlightSchedule -----------------------------------------------------
CREATE TABLE [schedule].[FlightSchedule] (
    ScheduleId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FlightSchedule_Id      DEFAULT NEWID(),
    FlightNumber    VARCHAR(10)      NOT NULL,
    Origin          CHAR(3)          NOT NULL,
    Destination     CHAR(3)          NOT NULL,
    DepartureTime   TIME             NOT NULL,
    ArrivalTime     TIME             NOT NULL,
    ArrivalDayOffset TINYINT         NOT NULL CONSTRAINT DF_FlightSchedule_Offset  DEFAULT 0,
    DaysOfWeek      TINYINT          NOT NULL,
    AircraftType    VARCHAR(4)       NOT NULL,
    ValidFrom       DATE             NOT NULL,
    ValidTo         DATE             NOT NULL,
    FlightsCreated  INT              NOT NULL CONSTRAINT DF_FlightSchedule_Flights DEFAULT 0,
    CabinFares      NVARCHAR(MAX)    NOT NULL,
    CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_FlightSchedule_Created DEFAULT SYSUTCDATETIME(),
    CreatedBy       VARCHAR(100)     NOT NULL,
    UpdatedAt       DATETIME2        NOT NULL CONSTRAINT DF_FlightSchedule_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_FlightSchedule   PRIMARY KEY (ScheduleId),
    CONSTRAINT CHK_FlightSchedule_CabinFares CHECK (ISJSON(CabinFares) = 1)
);

CREATE INDEX IX_FlightSchedule_FlightNumber
    ON [schedule].[FlightSchedule] (FlightNumber, ValidFrom, ValidTo);

GO
CREATE TRIGGER [schedule].[TR_FlightSchedule_UpdatedAt]
ON [schedule].[FlightSchedule]
AFTER UPDATE AS
    UPDATE [schedule].[FlightSchedule]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [schedule].[FlightSchedule] fs
    INNER JOIN inserted i ON fs.ScheduleId = i.ScheduleId;
GO

-- =============================================================================
-- CUSTOMER DOMAIN
-- =============================================================================

-- customer.TierConfig ---------------------------------------------------------
CREATE TABLE [customer].[TierConfig] (
    TierConfigId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TierConfig_Id      DEFAULT NEWID(),
    TierCode             VARCHAR(20)      NOT NULL,
    TierLabel            VARCHAR(50)      NOT NULL,
    MinQualifyingPoints  INT              NOT NULL,
    IsActive             BIT              NOT NULL CONSTRAINT DF_TierConfig_Active   DEFAULT 1,
    ValidFrom            DATETIME2        NOT NULL,
    ValidTo              DATETIME2            NULL,
    CreatedAt            DATETIME2        NOT NULL CONSTRAINT DF_TierConfig_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2        NOT NULL CONSTRAINT DF_TierConfig_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TierConfig PRIMARY KEY (TierConfigId)
);

CREATE INDEX IX_TierConfig_Active
    ON [customer].[TierConfig] (TierCode)
    WHERE IsActive = 1;

GO
CREATE TRIGGER [customer].[TR_TierConfig_UpdatedAt]
ON [customer].[TierConfig]
AFTER UPDATE AS
    UPDATE [customer].[TierConfig]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [customer].[TierConfig] tc
    INNER JOIN inserted i ON tc.TierConfigId = i.TierConfigId;
GO

-- customer.Customer -----------------------------------------------------------
CREATE TABLE [customer].[Customer] (
    CustomerId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Customer_Id       DEFAULT NEWID(),
    LoyaltyNumber       VARCHAR(20)      NOT NULL,
    IdentityReference   UNIQUEIDENTIFIER     NULL,
    GivenName           VARCHAR(100)     NOT NULL,
    Surname             VARCHAR(100)     NOT NULL,
    DateOfBirth         DATE                 NULL,
    Nationality         CHAR(3)              NULL,
    PreferredLanguage   CHAR(5)              NULL CONSTRAINT DF_Customer_Lang     DEFAULT 'en-GB',
    PhoneNumber         VARCHAR(30)          NULL,
    TierCode            VARCHAR(20)      NOT NULL CONSTRAINT DF_Customer_Tier     DEFAULT 'Blue',
    PointsBalance       INT              NOT NULL CONSTRAINT DF_Customer_Points   DEFAULT 0,
    TierProgressPoints  INT              NOT NULL CONSTRAINT DF_Customer_Tier2    DEFAULT 0,
    IsActive            BIT              NOT NULL CONSTRAINT DF_Customer_Active   DEFAULT 1,
    CreatedAt           DATETIME2        NOT NULL CONSTRAINT DF_Customer_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2        NOT NULL CONSTRAINT DF_Customer_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Customer          PRIMARY KEY (CustomerId),
    CONSTRAINT UQ_Customer_Loyalty  UNIQUE (LoyaltyNumber),
    CONSTRAINT UQ_Customer_Identity UNIQUE (IdentityReference)
);

CREATE INDEX IX_Customer_LoyaltyNumber ON [customer].[Customer] (LoyaltyNumber);
CREATE INDEX IX_Customer_Surname       ON [customer].[Customer] (Surname, GivenName);

GO
CREATE TRIGGER [customer].[TR_Customer_UpdatedAt]
ON [customer].[Customer]
AFTER UPDATE AS
    UPDATE [customer].[Customer]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [customer].[Customer] c
    INNER JOIN inserted i ON c.CustomerId = i.CustomerId;
GO

-- customer.LoyaltyTransaction -------------------------------------------------
CREATE TABLE [customer].[LoyaltyTransaction] (
    TransactionId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_LoyaltyTx_Id       DEFAULT NEWID(),
    CustomerId       UNIQUEIDENTIFIER NOT NULL,
    TransactionType  VARCHAR(20)      NOT NULL,
    PointsDelta      INT              NOT NULL,
    BalanceAfter     INT              NOT NULL,
    BookingReference CHAR(6)              NULL,
    FlightNumber     VARCHAR(10)          NULL,
    Description      VARCHAR(255)     NOT NULL,
    TransactionDate  DATETIME2        NOT NULL CONSTRAINT DF_LoyaltyTx_Date     DEFAULT SYSUTCDATETIME(),
    CreatedAt        DATETIME2        NOT NULL CONSTRAINT DF_LoyaltyTx_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2        NOT NULL CONSTRAINT DF_LoyaltyTx_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LoyaltyTransaction        PRIMARY KEY (TransactionId),
    CONSTRAINT FK_LoyaltyTransaction_Customer FOREIGN KEY (CustomerId) REFERENCES [customer].[Customer](CustomerId),
    CONSTRAINT CHK_LoyaltyTransaction_Type  CHECK (TransactionType IN ('Earn','Redeem','Adjustment','Expiry','Reinstate'))
);

CREATE INDEX IX_LoyaltyTransaction_Customer
    ON [customer].[LoyaltyTransaction] (CustomerId, TransactionDate DESC);

CREATE INDEX IX_LoyaltyTransaction_BookingReference
    ON [customer].[LoyaltyTransaction] (BookingReference)
    WHERE BookingReference IS NOT NULL;

GO
CREATE TRIGGER [customer].[TR_LoyaltyTransaction_UpdatedAt]
ON [customer].[LoyaltyTransaction]
AFTER UPDATE AS
    UPDATE [customer].[LoyaltyTransaction]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [customer].[LoyaltyTransaction] lt
    INNER JOIN inserted i ON lt.TransactionId = i.TransactionId;
GO

-- =============================================================================
-- IDENTITY DOMAIN
-- =============================================================================

-- identity.UserAccount --------------------------------------------------------
CREATE TABLE [identity].[UserAccount] (
    UserAccountId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UserAccount_Id      DEFAULT NEWID(),
    IdentityReference   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UserAccount_IdRef   DEFAULT NEWID(),
    Email               VARCHAR(254)     NOT NULL,
    PasswordHash        VARCHAR(255)     NOT NULL,
    IsEmailVerified     BIT              NOT NULL CONSTRAINT DF_UserAccount_Verified DEFAULT 0,
    IsLocked            BIT              NOT NULL CONSTRAINT DF_UserAccount_Locked   DEFAULT 0,
    FailedLoginAttempts TINYINT          NOT NULL CONSTRAINT DF_UserAccount_Failed   DEFAULT 0,
    LastLoginAt         DATETIME2            NULL,
    PasswordChangedAt   DATETIME2        NOT NULL CONSTRAINT DF_UserAccount_PwdAt   DEFAULT SYSUTCDATETIME(),
    CreatedAt           DATETIME2        NOT NULL CONSTRAINT DF_UserAccount_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2        NOT NULL CONSTRAINT DF_UserAccount_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_UserAccount         PRIMARY KEY (UserAccountId),
    CONSTRAINT UQ_UserAccount_Email   UNIQUE (Email),
    CONSTRAINT UQ_UserAccount_IdRef   UNIQUE (IdentityReference)
);

CREATE INDEX IX_UserAccount_Email ON [identity].[UserAccount] (Email);

GO
CREATE TRIGGER [identity].[TR_UserAccount_UpdatedAt]
ON [identity].[UserAccount]
AFTER UPDATE AS
    UPDATE [identity].[UserAccount]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [identity].[UserAccount] ua
    INNER JOIN inserted i ON ua.UserAccountId = i.UserAccountId;
GO

-- identity.RefreshToken -------------------------------------------------------
CREATE TABLE [identity].[RefreshToken] (
    RefreshTokenId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RefreshToken_Id       DEFAULT NEWID(),
    UserAccountId   UNIQUEIDENTIFIER NOT NULL,
    TokenHash       VARCHAR(255)     NOT NULL,
    DeviceHint      VARCHAR(100)         NULL,
    IsRevoked       BIT              NOT NULL CONSTRAINT DF_RefreshToken_Revoked  DEFAULT 0,
    ExpiresAt       DATETIME2        NOT NULL,
    CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_RefreshToken_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2        NOT NULL CONSTRAINT DF_RefreshToken_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_RefreshToken          PRIMARY KEY (RefreshTokenId),
    CONSTRAINT FK_RefreshToken_Account  FOREIGN KEY (UserAccountId) REFERENCES [identity].[UserAccount](UserAccountId)
);

CREATE INDEX IX_RefreshToken_UserAccount
    ON [identity].[RefreshToken] (UserAccountId)
    WHERE IsRevoked = 0;

GO
CREATE TRIGGER [identity].[TR_RefreshToken_UpdatedAt]
ON [identity].[RefreshToken]
AFTER UPDATE AS
    UPDATE [identity].[RefreshToken]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [identity].[RefreshToken] rt
    INNER JOIN inserted i ON rt.RefreshTokenId = i.RefreshTokenId;
GO

-- =============================================================================
-- DISRUPTION DOMAIN
-- =============================================================================

-- disruption.DisruptionEvent --------------------------------------------------
CREATE TABLE [disruption].[DisruptionEvent] (
    DisruptionEventId        NVARCHAR(100)    NOT NULL,
    EventType                NVARCHAR(20)     NOT NULL,
    FlightNumber             NVARCHAR(10)     NOT NULL,
    DepartureDate            DATE             NOT NULL,
    Status                   NVARCHAR(20)     NOT NULL CONSTRAINT DF_DisruptionEvent_Status DEFAULT 'Received',
    AffectedPassengerCount   INT              NOT NULL CONSTRAINT DF_DisruptionEvent_Affected DEFAULT 0,
    ProcessedPassengerCount  INT              NOT NULL CONSTRAINT DF_DisruptionEvent_Processed DEFAULT 0,
    Payload                  NVARCHAR(MAX)    NOT NULL,
    ReceivedAt               DATETIME2        NOT NULL CONSTRAINT DF_DisruptionEvent_Received DEFAULT SYSUTCDATETIME(),
    ProcessingStartedAt      DATETIME2            NULL,
    CompletedAt              DATETIME2            NULL,
    ErrorDetail              NVARCHAR(MAX)        NULL,
    CONSTRAINT PK_DisruptionEvent  PRIMARY KEY (DisruptionEventId),
    CONSTRAINT CHK_DisruptionEvent_Type   CHECK (EventType IN ('Delay','Cancellation')),
    CONSTRAINT CHK_DisruptionEvent_Status CHECK (Status IN ('Received','Processing','Completed','Failed'))
);

-- =============================================================================
-- ORDER DOMAIN — SSR Catalogue
-- =============================================================================

-- order.SsrCatalogue ----------------------------------------------------------
CREATE TABLE [order].[SsrCatalogue] (
    SsrCatalogueId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SsrCatalogue_Id      DEFAULT NEWID(),
    SsrCode         CHAR(4)          NOT NULL,
    Label           VARCHAR(100)     NOT NULL,
    Category        VARCHAR(20)      NOT NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_SsrCatalogue_Active   DEFAULT 1,
    CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_SsrCatalogue_Created  DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2        NOT NULL CONSTRAINT DF_SsrCatalogue_Updated  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_SsrCatalogue        PRIMARY KEY (SsrCatalogueId),
    CONSTRAINT UQ_SsrCatalogue_Code   UNIQUE (SsrCode),
    CONSTRAINT CHK_SsrCatalogue_Cat   CHECK (Category IN ('Meal','Mobility','Accessibility'))
);

CREATE INDEX IX_SsrCatalogue_Code
    ON [order].[SsrCatalogue] (SsrCode)
    WHERE IsActive = 1;

GO
CREATE TRIGGER [order].[TR_SsrCatalogue_UpdatedAt]
ON [order].[SsrCatalogue]
AFTER UPDATE AS
    UPDATE [order].[SsrCatalogue]
    SET    UpdatedAt = SYSUTCDATETIME()
    FROM   [order].[SsrCatalogue] sc
    INNER JOIN inserted i ON sc.SsrCatalogueId = i.SsrCatalogueId;
GO

PRINT 'All tables, triggers and indexes created.';

-- =============================================================================
-- =============================================================================
-- SEED / DEMO DATA
-- =============================================================================
-- =============================================================================

-- =============================================================================
-- seat.AircraftType
-- =============================================================================
INSERT INTO [seat].[AircraftType] (AircraftTypeCode, Manufacturer, FriendlyName, TotalSeats) VALUES
('A351', 'Airbus',  'Airbus A350-1000', 331),
('B789', 'Boeing',  'Boeing 787-9',     296),
('A339', 'Airbus',  'Airbus A330-900',  287);

-- =============================================================================
-- seat.SeatPricing  (W & Y only — J/F included in fare)
-- =============================================================================
INSERT INTO [seat].[SeatPricing] (CabinCode, SeatPosition, CurrencyCode, Price, ValidFrom) VALUES
('W', 'Window', 'GBP', 70.00, '2025-01-01'),
('W', 'Aisle',  'GBP', 50.00, '2025-01-01'),
('W', 'Middle', 'GBP', 20.00, '2025-01-01'),
('Y', 'Window', 'GBP', 70.00, '2025-01-01'),
('Y', 'Aisle',  'GBP', 50.00, '2025-01-01'),
('Y', 'Middle', 'GBP', 20.00, '2025-01-01');

-- =============================================================================
-- seat.Seatmap  — A351 (abbreviated layout for seed data)
-- =============================================================================
INSERT INTO [seat].[Seatmap] (AircraftTypeCode, CabinLayout) VALUES
('A351', N'{"aircraftType":"A351","version":1,"totalSeats":331,"cabins":[{"cabinCode":"J","cabinName":"Business Class","deckLevel":"Main","startRow":1,"endRow":8,"columns":["A","D","G","K"],"layout":"1-1-1-1","rows":[{"rowNumber":1,"seats":[{"seatNumber":"1A","column":"A","type":"Suite","position":"Window","attributes":["ExtraLegroom"],"isSelectable":true},{"seatNumber":"1D","column":"D","type":"Suite","position":"Middle","attributes":["ExtraLegroom"],"isSelectable":true},{"seatNumber":"1G","column":"G","type":"Suite","position":"Middle","attributes":["ExtraLegroom"],"isSelectable":true},{"seatNumber":"1K","column":"K","type":"Suite","position":"Window","attributes":["ExtraLegroom"],"isSelectable":true}]}]},{"cabinCode":"W","cabinName":"Premium Economy","deckLevel":"Main","startRow":11,"endRow":18,"columns":["A","B","D","E","F","H","K"],"layout":"2-3-2","rows":[{"rowNumber":11,"seats":[{"seatNumber":"11A","column":"A","type":"Standard","position":"Window","attributes":["ExtraLegroom"],"isSelectable":true},{"seatNumber":"11B","column":"B","type":"Standard","position":"Aisle","attributes":["ExtraLegroom"],"isSelectable":true},{"seatNumber":"11K","column":"K","type":"Standard","position":"Window","attributes":["ExtraLegroom"],"isSelectable":true}]}]},{"cabinCode":"Y","cabinName":"Economy","deckLevel":"Main","startRow":22,"endRow":54,"columns":["A","B","C","D","E","F","G","H","K"],"layout":"3-3-3","rows":[{"rowNumber":22,"seats":[{"seatNumber":"22A","column":"A","type":"Standard","position":"Window","attributes":[],"isSelectable":true},{"seatNumber":"22B","column":"B","type":"Standard","position":"Middle","attributes":[],"isSelectable":true},{"seatNumber":"22C","column":"C","type":"Standard","position":"Aisle","attributes":[],"isSelectable":true},{"seatNumber":"22D","column":"D","type":"Standard","position":"Aisle","attributes":[],"isSelectable":true},{"seatNumber":"22E","column":"E","type":"Standard","position":"Middle","attributes":[],"isSelectable":true},{"seatNumber":"22K","column":"K","type":"Standard","position":"Window","attributes":[],"isSelectable":true}]}]}]}');

-- =============================================================================
-- bag.BagPolicy
-- =============================================================================
INSERT INTO [bag].[BagPolicy] (CabinCode, FreeBagsIncluded, MaxWeightKgPerBag) VALUES
('F', 2, 32),
('J', 2, 32),
('W', 2, 23),
('Y', 1, 23);

-- =============================================================================
-- bag.BagPricing
-- =============================================================================
INSERT INTO [bag].[BagPricing]  (BagSequence, CurrencyCode, Price, ValidFrom) VALUES
(1,  'GBP',  60.00, '2025-01-01'),
(2,  'GBP',  80.00, '2025-01-01'),
(99, 'GBP', 100.00, '2025-01-01');

-- =============================================================================
-- order.SsrCatalogue
-- =============================================================================
INSERT INTO [order].[SsrCatalogue] (SsrCode, Label, Category) VALUES
('VGML', 'Vegetarian meal (lacto-ovo)',                          'Meal'),
('HNML', 'Hindu meal',                                           'Meal'),
('MOML', 'Muslim / halal meal',                                  'Meal'),
('KSML', 'Kosher meal',                                          'Meal'),
('DBML', 'Diabetic meal',                                        'Meal'),
('GFML', 'Gluten-free meal',                                     'Meal'),
('CHML', 'Child meal',                                           'Meal'),
('BBML', 'Baby / infant meal',                                   'Meal'),
('WCHR', 'Wheelchair — can walk, needs assistance over distances','Mobility'),
('WCHS', 'Wheelchair — cannot manage steps',                     'Mobility'),
('WCHC', 'Wheelchair — fully immobile',                          'Mobility'),
('BLND', 'Blind or severely visually impaired passenger',        'Accessibility'),
('DEAF', 'Deaf or severely hearing-impaired passenger',          'Accessibility'),
('DPNA', 'Disabled passenger needing assistance (general)',      'Accessibility');

-- =============================================================================
-- customer.TierConfig
-- =============================================================================
INSERT INTO [customer].[TierConfig] (TierCode, TierLabel, MinQualifyingPoints, ValidFrom) VALUES
('Blue',     'Apex Blue',     0,      '2025-01-01'),
('Silver',   'Apex Silver',   25000,  '2025-01-01'),
('Gold',     'Apex Gold',     50000,  '2025-01-01'),
('Platinum', 'Apex Platinum', 100000, '2025-01-01');

-- =============================================================================
-- identity.UserAccount  (PasswordHash is a placeholder Argon2id-format string)
-- =============================================================================
DECLARE @IdRef1 UNIQUEIDENTIFIER = NEWID();
DECLARE @IdRef2 UNIQUEIDENTIFIER = NEWID();
DECLARE @IdRef3 UNIQUEIDENTIFIER = NEWID();

INSERT INTO [identity].[UserAccount] (IdentityReference, Email, PasswordHash, IsEmailVerified) VALUES
(@IdRef1, 'amara.okafor@example.com',  '$argon2id$v=19$m=65536,t=3,p=4$c2FsdHZhbHVl$hash1placeholder==', 1),
(@IdRef2, 'james.chen@example.com',    '$argon2id$v=19$m=65536,t=3,p=4$c2FsdHZhbHVl$hash2placeholder==', 1),
(@IdRef3, 'priya.sharma@example.com',  '$argon2id$v=19$m=65536,t=3,p=4$c2FsdHZhbHVl$hash3placeholder==', 0);

-- =============================================================================
-- customer.Customer
-- =============================================================================
INSERT INTO [customer].[Customer]
    (LoyaltyNumber, IdentityReference, GivenName, Surname, DateOfBirth, Nationality, PreferredLanguage, PhoneNumber, TierCode, PointsBalance, TierProgressPoints)
VALUES
('AX9876543', @IdRef1, 'Amara',  'Okafor', '1988-03-22', 'GBR', 'en-GB', '+447700900123', 'Silver',   48250,  62100),
('AX1234567', @IdRef2, 'James',  'Chen',   '1979-11-05', 'GBR', 'en-GB', '+447700900456', 'Gold',    112800, 138400),
('AX5554321', @IdRef3, 'Priya',  'Sharma', '1995-07-14', 'IND', 'en-GB', '+447700900789', 'Blue',      3250,   3250);

-- =============================================================================
-- customer.LoyaltyTransaction  (sample history for Amara)
-- =============================================================================
DECLARE @CustId1 UNIQUEIDENTIFIER = (SELECT CustomerId FROM [customer].[Customer] WHERE LoyaltyNumber = 'AX9876543');
DECLARE @CustId2 UNIQUEIDENTIFIER = (SELECT CustomerId FROM [customer].[Customer] WHERE LoyaltyNumber = 'AX1234567');

INSERT INTO [customer].[LoyaltyTransaction]
    (CustomerId, TransactionType, PointsDelta, BalanceAfter, BookingReference, FlightNumber, Description, TransactionDate)
VALUES
-- Amara's history
(@CustId1, 'Earn',       13836,  13836, 'AB1234', 'AX001', 'Points earned — AX001 LHR-JFK, Business Flex (Platinum bonus applied)',  '2025-06-15T14:22:00Z'),
(@CustId1, 'Earn',        6918,  20754, 'XK7T2P', 'AX002', 'Points earned — AX002 JFK-LHR, Business Flex',                          '2025-08-02T09:10:00Z'),
(@CustId1, 'Earn',       13836,  34590, 'LM3R7Q', 'AX001', 'Points earned — AX001 LHR-JFK, Business Flex',                          '2025-09-20T11:45:00Z'),
(@CustId1, 'Adjustment',  2500,  37090, NULL,     NULL,    'Goodwill gesture — disruption on AX301',                                '2025-10-01T08:00:00Z'),
(@CustId1, 'Earn',        8750,  45840, 'PR9W4N', 'AX003', 'Points earned — AX003 LHR-JFK, Business Flex',                          '2025-11-02T14:22:00Z'),
(@CustId1, 'Earn',        6918,  52758, 'PR9W4N', 'AX004', 'Points earned — AX004 JFK-LHR, Business Flex',                          '2025-11-10T09:30:00Z'),
(@CustId1, 'Redeem',     -5000,  47758, 'ZZ9K1M', NULL,    'Upgrade to Business Class — AX301 LHR-SIN',                            '2025-12-18T10:00:00Z'),
(@CustId1, 'Earn',        8750,  56508, 'ZZ9K1M', 'AX301', 'Points earned — AX301 LHR-SIN, Business Class',                         '2026-01-05T17:00:00Z'),
-- James's history
(@CustId2, 'Earn',       20754, 20754,  'JC0001', 'AX001', 'Points earned — AX001 LHR-JFK, Business Flex (Gold bonus applied)',      '2025-03-10T08:00:00Z'),
(@CustId2, 'Earn',       41508, 62262,  'JC0002', 'AX211', 'Points earned — AX211 LHR-HKG, First Class (Gold bonus applied)',        '2025-05-20T22:00:00Z'),
(@CustId2, 'Earn',       20754, 83016,  'JC0003', 'AX002', 'Points earned — AX002 JFK-LHR, Business Flex',                          '2025-07-14T13:30:00Z'),
(@CustId2, 'Earn',       20754, 103770, 'JC0004', 'AX001', 'Points earned — AX001 LHR-JFK, Business Flex',                          '2025-09-28T09:00:00Z'),
(@CustId2, 'Earn',        9030, 112800, 'JC0005', 'AX411', 'Points earned — AX411 LHR-DEL, Business Flex',                          '2025-11-15T20:30:00Z');

-- =============================================================================
-- schedule.FlightSchedule  — two representative schedules
-- =============================================================================
INSERT INTO [schedule].[FlightSchedule]
    (FlightNumber, Origin, Destination, DepartureTime, ArrivalTime, ArrivalDayOffset,
     DaysOfWeek, AircraftType, ValidFrom, ValidTo, FlightsCreated, CabinFares, CreatedBy)
VALUES
-- AX001 LHR→JFK  daily  A351
('AX001', 'LHR', 'JFK', '08:00', '11:10', 0, 127, 'A351', '2026-01-01', '2026-12-31', 365,
 N'{"cabins":[{"cabinCode":"J","totalSeats":48,"fares":[{"fareBasisCode":"JFLEXGB","fareFamily":"Business Flex","currencyCode":"GBP","baseFareAmount":1250.00,"taxAmount":182.50,"isRefundable":true,"isChangeable":true,"changeFeeAmount":0.00,"cancellationFeeAmount":0.00,"pointsPrice":125000,"pointsTaxes":182.50},{"fareBasisCode":"JSAVERGB","fareFamily":"Business Saver","currencyCode":"GBP","baseFareAmount":950.00,"taxAmount":182.50,"isRefundable":false,"isChangeable":true,"changeFeeAmount":150.00,"cancellationFeeAmount":0.00,"pointsPrice":95000,"pointsTaxes":182.50}]},{"cabinCode":"W","totalSeats":56,"fares":[{"fareBasisCode":"WFLEXGB","fareFamily":"Premium Economy Flex","currencyCode":"GBP","baseFareAmount":650.00,"taxAmount":130.00,"isRefundable":true,"isChangeable":true,"changeFeeAmount":0.00,"cancellationFeeAmount":0.00,"pointsPrice":65000,"pointsTaxes":130.00}]},{"cabinCode":"Y","totalSeats":227,"fares":[{"fareBasisCode":"YFLEXGB","fareFamily":"Economy Flex","currencyCode":"GBP","baseFareAmount":350.00,"taxAmount":97.25,"isRefundable":true,"isChangeable":true,"changeFeeAmount":0.00,"cancellationFeeAmount":0.00,"pointsPrice":35000,"pointsTaxes":97.25},{"fareBasisCode":"YLOWUK","fareFamily":"Economy Light","currencyCode":"GBP","baseFareAmount":149.00,"taxAmount":97.25,"isRefundable":false,"isChangeable":false,"changeFeeAmount":0.00,"cancellationFeeAmount":149.00,"pointsPrice":null,"pointsTaxes":null}]}]}',
 'ops-admin@apexair.com'),
-- AX002 JFK→LHR  daily  A351
('AX002', 'JFK', 'LHR', '13:00', '01:15', 1, 127, 'A351', '2026-01-01', '2026-12-31', 365,
 N'{"cabins":[{"cabinCode":"J","totalSeats":48,"fares":[{"fareBasisCode":"JFLEXGB","fareFamily":"Business Flex","currencyCode":"GBP","baseFareAmount":1250.00,"taxAmount":182.50,"isRefundable":true,"isChangeable":true,"changeFeeAmount":0.00,"cancellationFeeAmount":0.00,"pointsPrice":125000,"pointsTaxes":182.50}]},{"cabinCode":"Y","totalSeats":227,"fares":[{"fareBasisCode":"YFLEXGB","fareFamily":"Economy Flex","currencyCode":"GBP","baseFareAmount":350.00,"taxAmount":97.25,"isRefundable":true,"isChangeable":true,"changeFeeAmount":0.00,"cancellationFeeAmount":0.00,"pointsPrice":35000,"pointsTaxes":97.25},{"fareBasisCode":"YLOWUK","fareFamily":"Economy Light","currencyCode":"GBP","baseFareAmount":149.00,"taxAmount":97.25,"isRefundable":false,"isChangeable":false,"changeFeeAmount":0.00,"cancellationFeeAmount":149.00,"pointsPrice":null,"pointsTaxes":null}]}]}',
 'ops-admin@apexair.com');

-- =============================================================================
-- offer.FlightInventory  — two flights for 15-Aug-2026
-- =============================================================================
DECLARE @InvId_AX001_J  UNIQUEIDENTIFIER = NEWID();
DECLARE @InvId_AX001_Y  UNIQUEIDENTIFIER = NEWID();
DECLARE @InvId_AX002_J  UNIQUEIDENTIFIER = NEWID();
DECLARE @InvId_AX002_Y  UNIQUEIDENTIFIER = NEWID();
DECLARE @InvId_AX411_J  UNIQUEIDENTIFIER = NEWID();
DECLARE @InvId_AX411_Y  UNIQUEIDENTIFIER = NEWID();
DECLARE @InvId_AX301_J  UNIQUEIDENTIFIER = NEWID();

INSERT INTO [offer].[FlightInventory]
    (InventoryId, FlightNumber, DepartureDate, DepartureTime, ArrivalTime, ArrivalDayOffset,
     Origin, Destination, AircraftType, CabinCode, TotalSeats, SeatsAvailable, SeatsSold, SeatsHeld)
VALUES
(@InvId_AX001_J, 'AX001', '2026-08-15', '08:00', '11:10', 0, 'LHR', 'JFK', 'A351', 'J', 48, 44, 4, 0),
(@InvId_AX001_Y, 'AX001', '2026-08-15', '08:00', '11:10', 0, 'LHR', 'JFK', 'A351', 'Y', 227, 189, 32, 6),
(@InvId_AX002_J, 'AX002', '2026-08-25', '13:00', '01:15', 1, 'JFK', 'LHR', 'A351', 'J', 48, 45, 3, 0),
(@InvId_AX002_Y, 'AX002', '2026-08-25', '13:00', '01:15', 1, 'JFK', 'LHR', 'A351', 'Y', 227, 200, 22, 5),
(@InvId_AX411_J, 'AX411', '2026-09-10', '20:30', '09:00', 1, 'LHR', 'DEL', 'B789', 'J', 42, 38, 4, 0),
(@InvId_AX411_Y, 'AX411', '2026-09-10', '20:30', '09:00', 1, 'LHR', 'DEL', 'B789', 'Y', 194, 150, 40, 4),
(@InvId_AX301_J, 'AX301', '2026-10-01', '21:30', '17:45', 1, 'LHR', 'SIN', 'A351', 'J', 48, 42, 6, 0);

-- =============================================================================
-- offer.Fare  — one flex and one light per inventory record (sample)
-- =============================================================================
DECLARE @FareId_AX001_J_Flex  UNIQUEIDENTIFIER = NEWID();
DECLARE @FareId_AX001_Y_Flex  UNIQUEIDENTIFIER = NEWID();
DECLARE @FareId_AX001_Y_Light UNIQUEIDENTIFIER = NEWID();

INSERT INTO [offer].[Fare]
    (FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
     BaseFareAmount, TaxAmount, TotalAmount, IsRefundable, IsChangeable,
     ChangeFeeAmount, CancellationFeeAmount, PointsPrice, PointsTaxes,
     ValidFrom, ValidTo)
VALUES
(@FareId_AX001_J_Flex,  @InvId_AX001_J, 'JFLEXGB', 'Business Flex',  'J', 'J', 1250.00, 182.50, 1432.50, 1, 1, 0.00, 0.00,   125000, 182.50, '2025-01-01', '2026-12-31'),
(@FareId_AX001_Y_Flex,  @InvId_AX001_Y, 'YFLEXGB', 'Economy Flex',   'Y', 'Y',  350.00,  97.25,  447.25, 1, 1, 0.00, 0.00,    35000,  97.25, '2025-01-01', '2026-12-31'),
(@FareId_AX001_Y_Light, @InvId_AX001_Y, 'YLOWUK',  'Economy Light',  'Y', 'Y',  149.00,  97.25,  246.25, 0, 0, 0.00, 149.00,    NULL,  NULL,  '2025-01-01', '2026-12-31');

-- =============================================================================
-- offer.StoredOffer  — pre-created offers for demo booking below
-- =============================================================================
DECLARE @OfferId_Out UNIQUEIDENTIFIER = NEWID();
DECLARE @OfferId_In  UNIQUEIDENTIFIER = NEWID();

INSERT INTO [offer].[StoredOffer]
    (OfferId, InventoryId, FareId, FlightNumber, DepartureDate, Origin, Destination,
     FareBasisCode, FareFamily, BaseFareAmount, TaxAmount, TotalAmount,
     IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
     PointsPrice, PointsTaxes, BookingType, ExpiresAt, IsConsumed)
VALUES
(@OfferId_Out, @InvId_AX001_J, @FareId_AX001_J_Flex, 'AX001', '2026-08-15', 'LHR', 'JFK',
 'JFLEXGB', 'Business Flex', 1250.00, 182.50, 1432.50, 1, 1, 0.00, 0.00, 125000, 182.50, 'Revenue',
 DATEADD(MINUTE, 60, SYSUTCDATETIME()), 1),

(@OfferId_In,  @InvId_AX002_J, @FareId_AX001_J_Flex, 'AX002', '2026-08-25', 'JFK', 'LHR',
 'JFLEXGB', 'Business Flex', 1250.00, 182.50, 1432.50, 1, 1, 0.00, 0.00, 125000, 182.50, 'Revenue',
 DATEADD(MINUTE, 60, SYSUTCDATETIME()), 1);

-- =============================================================================
-- payment.Payment  — fare payment for demo booking
-- =============================================================================
DECLARE @PayId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @PayId2 UNIQUEIDENTIFIER = NEWID();

INSERT INTO [payment].[Payment]
    (PaymentId, PaymentReference, BookingReference, PaymentType, Method, CardType, CardLast4,
     AuthorisedAmount, SettledAmount, Status, SettledAt, Description)
VALUES
(@PayId1, 'AXPAY-0001', 'AB1234', 'Fare', 'CreditCard', 'Visa', '4242',
 2865.00, 2865.00, 'Settled', SYSUTCDATETIME(),
 'Fare — AX001 LHR-JFK + AX002 JFK-LHR, Business Flex, 2 PAX'),

(@PayId2, 'AXPAY-0002', 'AB1234', 'BagAncillary', 'CreditCard', 'Visa', '4242',
 60.00, 60.00, 'Settled', SYSUTCDATETIME(),
 'Bag ancillary — AX001 LHR-JFK, PAX-1, 1 additional bag');

-- payment.PaymentEvent
INSERT INTO [payment].[PaymentEvent] (PaymentId, EventType, Amount, Notes) VALUES
(@PayId1, 'Authorised', 2865.00, 'Fare authorisation — Visa 4242'),
(@PayId1, 'Settled',    2865.00, 'Fare settled at order confirmation'),
(@PayId2, 'Authorised',   60.00, 'Bag ancillary authorisation'),
(@PayId2, 'Settled',      60.00, 'Bag ancillary settled after order confirmation');

-- =============================================================================
-- order.Order  — one confirmed return booking for Amara + Jordan
-- =============================================================================
DECLARE @OrderId1 UNIQUEIDENTIFIER = NEWID();

INSERT INTO [order].[Order]
    (OrderId, BookingReference, OrderStatus, ChannelCode, CurrencyCode,
     TicketingTimeLimit, TotalAmount, OrderData)
VALUES
(@OrderId1, 'AB1234', 'Confirmed', 'WEB', 'GBP', '2026-08-15T08:00:00Z', 2925.00,
N'{
  "dataLists": {
    "passengers": [
      {
        "passengerId": "PAX-1",
        "type": "ADT",
        "givenName": "Amara",
        "surname": "Okafor",
        "dateOfBirth": "1988-03-22",
        "gender": "Female",
        "loyaltyNumber": "AX9876543",
        "contacts": { "email": "amara.okafor@example.com", "phone": "+447700900123" },
        "travelDocument": { "type": "PASSPORT", "number": "PA1234567", "issuingCountry": "GBR", "expiryDate": "2030-01-01", "nationality": "GBR" }
      },
      {
        "passengerId": "PAX-2",
        "type": "ADT",
        "givenName": "Jordan",
        "surname": "Taylor",
        "dateOfBirth": "1987-07-22",
        "gender": "Male",
        "loyaltyNumber": null,
        "contacts": null,
        "travelDocument": { "type": "PASSPORT", "number": "PA7654321", "issuingCountry": "GBR", "expiryDate": "2028-06-30", "nationality": "GBR" }
      }
    ],
    "flightSegments": [
      {
        "segmentId": "SEG-1",
        "flightNumber": "AX001",
        "origin": "LHR",
        "destination": "JFK",
        "departureDateTime": "2026-08-15T08:00:00Z",
        "arrivalDateTime": "2026-08-15T11:10:00Z",
        "aircraftType": "A351",
        "operatingCarrier": "AX",
        "marketingCarrier": "AX",
        "cabinCode": "J",
        "bookingClass": "J"
      },
      {
        "segmentId": "SEG-2",
        "flightNumber": "AX002",
        "origin": "JFK",
        "destination": "LHR",
        "departureDateTime": "2026-08-25T13:00:00Z",
        "arrivalDateTime": "2026-08-26T01:15:00Z",
        "aircraftType": "A351",
        "operatingCarrier": "AX",
        "marketingCarrier": "AX",
        "cabinCode": "J",
        "bookingClass": "J"
      }
    ]
  },
  "orderItems": [
    {
      "orderItemId": "OI-1", "type": "Flight", "segmentRef": "SEG-1",
      "passengerRefs": ["PAX-1","PAX-2"], "offerId": "see-stored-offer",
      "fareBasisCode": "JFLEXGB", "fareFamily": "Business Flex",
      "unitPrice": 1250.00, "taxes": 182.50, "totalPrice": 1432.50,
      "isRefundable": true, "isChangeable": true, "paymentReference": "AXPAY-0001",
      "eTickets": [
        { "passengerId": "PAX-1", "eTicketNumber": "932-1000000001" },
        { "passengerId": "PAX-2", "eTicketNumber": "932-1000000002" }
      ],
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "1A" },
        { "passengerId": "PAX-2", "seatNumber": "1K" }
      ]
    },
    {
      "orderItemId": "OI-2", "type": "Flight", "segmentRef": "SEG-2",
      "passengerRefs": ["PAX-1","PAX-2"], "offerId": "see-stored-offer",
      "fareBasisCode": "JFLEXGB", "fareFamily": "Business Flex",
      "unitPrice": 1250.00, "taxes": 182.50, "totalPrice": 1432.50,
      "isRefundable": true, "isChangeable": true, "paymentReference": "AXPAY-0001",
      "eTickets": [
        { "passengerId": "PAX-1", "eTicketNumber": "932-1000000003" },
        { "passengerId": "PAX-2", "eTicketNumber": "932-1000000004" }
      ],
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "2A" },
        { "passengerId": "PAX-2", "seatNumber": "2K" }
      ]
    },
    {
      "orderItemId": "OI-3", "type": "Bag", "segmentRef": "SEG-1",
      "passengerRefs": ["PAX-1"],
      "bagOfferId": "bo-business-bag1-v1", "freeBagsIncluded": 2,
      "additionalBags": 1, "bagSequence": 1,
      "unitPrice": 60.00, "taxes": 0.00, "totalPrice": 60.00,
      "paymentReference": "AXPAY-0002"
    }
  ],
  "payments": [
    {
      "paymentReference": "AXPAY-0001", "description": "Fare — LHR-JFK-LHR, Business Flex, 2 PAX",
      "method": "CreditCard", "cardLast4": "4242", "cardType": "Visa",
      "authorisedAmount": 2865.00, "settledAmount": 2865.00, "currency": "GBP",
      "status": "Settled", "authorisedAt": "2026-03-17T10:30:00Z", "settledAt": "2026-03-17T10:31:00Z"
    },
    {
      "paymentReference": "AXPAY-0002", "description": "Bag ancillary — SEG-1, PAX-1",
      "method": "CreditCard", "cardLast4": "4242", "cardType": "Visa",
      "authorisedAmount": 60.00, "settledAmount": 60.00, "currency": "GBP",
      "status": "Settled", "authorisedAt": "2026-03-17T10:30:00Z", "settledAt": "2026-03-17T10:32:00Z"
    }
  ],
  "history": [
    { "event": "OrderCreated",   "at": "2026-03-17T10:29:00Z", "by": "WEB" },
    { "event": "OrderConfirmed", "at": "2026-03-17T10:31:00Z", "by": "WEB" },
    { "event": "BagAncillaryAdded", "at": "2026-03-17T10:32:00Z", "by": "WEB" }
  ]
}');

-- =============================================================================
-- delivery.Ticket  — four e-tickets for booking AB1234
-- =============================================================================
DECLARE @TktId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @TktId2 UNIQUEIDENTIFIER = NEWID();
DECLARE @TktId3 UNIQUEIDENTIFIER = NEWID();
DECLARE @TktId4 UNIQUEIDENTIFIER = NEWID();

INSERT INTO [delivery].[Ticket]
    (TicketId, ETicketNumber, InventoryId, FlightNumber, DepartureDate,
     BookingReference, PassengerId, GivenName, Surname, CabinCode, FareBasisCode, TicketData)
VALUES
(@TktId1, '932-1000000001', @InvId_AX001_J, 'AX001', '2026-08-15', 'AB1234', 'PAX-1', 'Amara',  'Okafor', 'J', 'JFLEXGB',
 N'{"seatAssignment":{"seatNumber":"1A","positionType":"Window","deckCode":"M"},"ssrCodes":[],"apisData":null,"changeHistory":[{"eventType":"Issued","occurredAt":"2026-03-17T10:31:00Z","actor":"RetailAPI","detail":"Initial ticket issuance"}]}'),

(@TktId2, '932-1000000002', @InvId_AX001_J, 'AX001', '2026-08-15', 'AB1234', 'PAX-2', 'Jordan', 'Taylor',  'J', 'JFLEXGB',
 N'{"seatAssignment":{"seatNumber":"1K","positionType":"Window","deckCode":"M"},"ssrCodes":[],"apisData":null,"changeHistory":[{"eventType":"Issued","occurredAt":"2026-03-17T10:31:00Z","actor":"RetailAPI","detail":"Initial ticket issuance"}]}'),

(@TktId3, '932-1000000003', @InvId_AX002_J, 'AX002', '2026-08-25', 'AB1234', 'PAX-1', 'Amara',  'Okafor', 'J', 'JFLEXGB',
 N'{"seatAssignment":{"seatNumber":"2A","positionType":"Window","deckCode":"M"},"ssrCodes":[],"apisData":null,"changeHistory":[{"eventType":"Issued","occurredAt":"2026-03-17T10:31:00Z","actor":"RetailAPI","detail":"Initial ticket issuance"}]}'),

(@TktId4, '932-1000000004', @InvId_AX002_J, 'AX002', '2026-08-25', 'AB1234', 'PAX-2', 'Jordan', 'Taylor',  'J', 'JFLEXGB',
 N'{"seatAssignment":{"seatNumber":"2K","positionType":"Window","deckCode":"M"},"ssrCodes":[],"apisData":null,"changeHistory":[{"eventType":"Issued","occurredAt":"2026-03-17T10:31:00Z","actor":"RetailAPI","detail":"Initial ticket issuance"}]}');

-- =============================================================================
-- delivery.Manifest  — manifest entries for booking AB1234
-- =============================================================================
INSERT INTO [delivery].[Manifest]
    (TicketId, InventoryId, FlightNumber, DepartureDate, AircraftType,
     SeatNumber, CabinCode, BookingReference, ETicketNumber,
     PassengerId, GivenName, Surname, DepartureTime, ArrivalTime)
VALUES
(@TktId1, @InvId_AX001_J, 'AX001', '2026-08-15', 'A351', '1A',  'J', 'AB1234', '932-1000000001', 'PAX-1', 'Amara',  'Okafor', '08:00', '11:10'),
(@TktId2, @InvId_AX001_J, 'AX001', '2026-08-15', 'A351', '1K',  'J', 'AB1234', '932-1000000002', 'PAX-2', 'Jordan', 'Taylor', '08:00', '11:10'),
(@TktId3, @InvId_AX002_J, 'AX002', '2026-08-25', 'A351', '2A',  'J', 'AB1234', '932-1000000003', 'PAX-1', 'Amara',  'Okafor', '13:00', '01:15'),
(@TktId4, @InvId_AX002_J, 'AX002', '2026-08-25', 'A351', '2K',  'J', 'AB1234', '932-1000000004', 'PAX-2', 'Jordan', 'Taylor', '13:00', '01:15');

-- =============================================================================
-- delivery.Document  — bag ancillary EMD
-- =============================================================================
INSERT INTO [delivery].[Document]
    (DocumentNumber, DocumentType, BookingReference, ETicketNumber, PassengerId,
     SegmentRef, PaymentReference, Amount, DocumentData)
VALUES
('932-EMD-0000001', 'BagAncillary', 'AB1234', '932-1000000001', 'PAX-1',
 'SEG-1', 'AXPAY-0002', 60.00,
 N'{"emdType":"EMD-A","rfic":"C","rfisc":"0GO","serviceDescription":"Additional Checked Bag — 23 kg","couponStatus":"Open","ancillaryDetail":{"type":"BagAncillary","bagSequenceNumber":1,"weightKg":23,"dimensionsCm":{"length":90,"width":75,"depth":43},"bagTagNumber":null},"priceBreakdown":{"baseAmount":60.00,"taxes":[],"totalAmount":60.00,"currencyCode":"GBP"},"voidHistory":[]}');

-- =============================================================================
-- A second booking — James Chen  LHR→DEL Economy Light (single PAX)
-- =============================================================================
DECLARE @OrderId2  UNIQUEIDENTIFIER = NEWID();
DECLARE @PayId3    UNIQUEIDENTIFIER = NEWID();
DECLARE @FareId_AX411_Y_Light UNIQUEIDENTIFIER = NEWID();
DECLARE @OfferId_DEL UNIQUEIDENTIFIER = NEWID();
DECLARE @TktId5    UNIQUEIDENTIFIER = NEWID();

INSERT INTO [offer].[Fare]
    (FareId, InventoryId, FareBasisCode, FareFamily, CabinCode, BookingClass,
     BaseFareAmount, TaxAmount, TotalAmount, IsRefundable, IsChangeable,
     ChangeFeeAmount, CancellationFeeAmount, ValidFrom, ValidTo)
VALUES
(@FareId_AX411_Y_Light, @InvId_AX411_Y, 'YLOWUK', 'Economy Light', 'Y', 'Y',
 199.00, 110.50, 309.50, 0, 0, 0.00, 199.00, '2025-01-01', '2026-12-31');

INSERT INTO [offer].[StoredOffer]
    (OfferId, InventoryId, FareId, FlightNumber, DepartureDate, Origin, Destination,
     FareBasisCode, FareFamily, BaseFareAmount, TaxAmount, TotalAmount,
     IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount, BookingType,
     ExpiresAt, IsConsumed)
VALUES
(@OfferId_DEL, @InvId_AX411_Y, @FareId_AX411_Y_Light, 'AX411', '2026-09-10', 'LHR', 'DEL',
 'YLOWUK', 'Economy Light', 199.00, 110.50, 309.50, 0, 0, 0.00, 199.00, 'Revenue',
 DATEADD(MINUTE, 60, SYSUTCDATETIME()), 1);

INSERT INTO [payment].[Payment]
    (PaymentId, PaymentReference, BookingReference, PaymentType, Method, CardType, CardLast4,
     AuthorisedAmount, SettledAmount, Status, SettledAt, Description)
VALUES
(@PayId3, 'AXPAY-0003', 'JC0005', 'Fare', 'CreditCard', 'Mastercard', '1234',
 309.50, 309.50, 'Settled', SYSUTCDATETIME(), 'Fare — AX411 LHR-DEL, Economy Light, 1 PAX');

INSERT INTO [payment].[PaymentEvent] (PaymentId, EventType, Amount, Notes) VALUES
(@PayId3, 'Authorised', 309.50, 'Fare authorisation'),
(@PayId3, 'Settled',    309.50, 'Settled at confirmation');

INSERT INTO [order].[Order]
    (OrderId, BookingReference, OrderStatus, ChannelCode, CurrencyCode,
     TicketingTimeLimit, TotalAmount, OrderData)
VALUES
(@OrderId2, 'JC0005', 'Confirmed', 'APP', 'GBP', '2026-09-10T20:30:00Z', 309.50,
N'{
  "dataLists": {
    "passengers": [
      {
        "passengerId": "PAX-1", "type": "ADT", "givenName": "James", "surname": "Chen",
        "dateOfBirth": "1979-11-05", "gender": "Male", "loyaltyNumber": "AX1234567",
        "contacts": { "email": "james.chen@example.com", "phone": "+447700900456" },
        "travelDocument": { "type": "PASSPORT", "number": "PB9876543", "issuingCountry": "GBR", "expiryDate": "2031-05-30", "nationality": "GBR" }
      }
    ],
    "flightSegments": [
      {
        "segmentId": "SEG-1", "flightNumber": "AX411", "origin": "LHR", "destination": "DEL",
        "departureDateTime": "2026-09-10T20:30:00Z", "arrivalDateTime": "2026-09-11T09:00:00Z",
        "aircraftType": "B789", "operatingCarrier": "AX", "marketingCarrier": "AX",
        "cabinCode": "Y", "bookingClass": "Y"
      }
    ]
  },
  "orderItems": [
    {
      "orderItemId": "OI-1", "type": "Flight", "segmentRef": "SEG-1",
      "passengerRefs": ["PAX-1"],
      "fareBasisCode": "YLOWUK", "fareFamily": "Economy Light",
      "unitPrice": 199.00, "taxes": 110.50, "totalPrice": 309.50,
      "isRefundable": false, "isChangeable": false, "paymentReference": "AXPAY-0003",
      "eTickets": [{ "passengerId": "PAX-1", "eTicketNumber": "932-1000000005" }],
      "seatAssignments": [{ "passengerId": "PAX-1", "seatNumber": "22A" }]
    }
  ],
  "payments": [
    {
      "paymentReference": "AXPAY-0003", "description": "Fare — LHR-DEL, Economy Light, 1 PAX",
      "method": "CreditCard", "cardLast4": "1234", "cardType": "Mastercard",
      "authorisedAmount": 309.50, "settledAmount": 309.50, "currency": "GBP",
      "status": "Settled", "authorisedAt": "2026-05-01T09:15:00Z", "settledAt": "2026-05-01T09:16:00Z"
    }
  ],
  "history": [
    { "event": "OrderCreated",   "at": "2026-05-01T09:14:00Z", "by": "APP" },
    { "event": "OrderConfirmed", "at": "2026-05-01T09:16:00Z", "by": "APP" }
  ]
}');

INSERT INTO [delivery].[Ticket]
    (TicketId, ETicketNumber, InventoryId, FlightNumber, DepartureDate,
     BookingReference, PassengerId, GivenName, Surname, CabinCode, FareBasisCode, TicketData)
VALUES
(@TktId5, '932-1000000005', @InvId_AX411_Y, 'AX411', '2026-09-10', 'JC0005', 'PAX-1', 'James', 'Chen', 'Y', 'YLOWUK',
 N'{"seatAssignment":{"seatNumber":"22A","positionType":"Window","deckCode":"M"},"ssrCodes":[],"apisData":null,"changeHistory":[{"eventType":"Issued","occurredAt":"2026-05-01T09:16:00Z","actor":"RetailAPI","detail":"Initial ticket issuance"}]}');

INSERT INTO [delivery].[Manifest]
    (TicketId, InventoryId, FlightNumber, DepartureDate, AircraftType,
     SeatNumber, CabinCode, BookingReference, ETicketNumber,
     PassengerId, GivenName, Surname, DepartureTime, ArrivalTime)
VALUES
(@TktId5, @InvId_AX411_Y, 'AX411', '2026-09-10', 'B789', '22A', 'Y', 'JC0005', '932-1000000005',
 'PAX-1', 'James', 'Chen', '20:30', '09:00');

-- =============================================================================
-- disruption.DisruptionEvent  — sample historical delay on AX301
-- =============================================================================
INSERT INTO [disruption].[DisruptionEvent]
    (DisruptionEventId, EventType, FlightNumber, DepartureDate, Status,
     AffectedPassengerCount, ProcessedPassengerCount, Payload,
     ReceivedAt, ProcessingStartedAt, CompletedAt)
VALUES
('FOS-2026-03-01-AX301-DEL-001', 'Delay', 'AX301', '2026-03-01', 'Completed',
 285, 285,
 N'{"disruptionEventId":"FOS-2026-03-01-AX301-DEL-001","eventType":"Delay","flightNumber":"AX301","departureDate":"2026-03-01","originalDepartureTime":"21:30","newDepartureTime":"23:45","newArrivalTime":"20:00","delayMinutes":135,"reason":"Aircraft technical inspection"}',
 '2026-03-01T18:30:00Z', '2026-03-01T18:30:05Z', '2026-03-01T18:35:22Z');

PRINT 'Seed data inserted successfully.';

-- =============================================================================
-- COMMIT
-- =============================================================================
COMMIT TRANSACTION;

PRINT '=== Apex Air schema and seed data applied successfully. ===';

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrLine INT            = ERROR_LINE();
    DECLARE @ErrProc NVARCHAR(200)  = ISNULL(ERROR_PROCEDURE(), 'N/A');

    PRINT '=== ERROR — transaction rolled back ===';
    PRINT 'Procedure : ' + @ErrProc;
    PRINT 'Line      : ' + CAST(@ErrLine AS NVARCHAR(10));
    PRINT 'Message   : ' + @ErrMsg;

    THROW;

END CATCH;
