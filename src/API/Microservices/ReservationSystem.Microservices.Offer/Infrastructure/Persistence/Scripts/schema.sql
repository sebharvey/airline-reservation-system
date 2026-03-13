-- =============================================================================
-- Schema: offer
-- Naming convention: [domain].[Table] — each API owns its own schema.
-- Cross-schema foreign keys are prohibited; integrity is enforced at
-- the application layer per the data principles.
-- =============================================================================

CREATE SCHEMA [offer];
GO

-- =============================================================================
-- Table: offer.Offers
--
-- Columns:
--   Id            UNIQUEIDENTIFIER  PK — application-generated via Guid.NewGuid()
--   FlightNumber  NVARCHAR(10)      IATA/ICAO flight number, e.g. 'BA0123'
--   Origin        NVARCHAR(3)       IATA airport code for departure, e.g. 'LHR'
--   Destination   NVARCHAR(3)       IATA airport code for arrival, e.g. 'JFK'
--   DepartureAt   DATETIME2(7)      UTC scheduled departure time
--   FareClass     NVARCHAR(50)      Fare bucket: 'economy' | 'premium_economy' | 'business' | 'first'
--   TotalPrice    DECIMAL(18,2)     Priced in Currency
--   Currency      NVARCHAR(3)       ISO 4217 currency code, e.g. 'GBP'
--   Status        NVARCHAR(50)      Lifecycle: 'available' | 'sold' | 'expired'
--   Attributes    NVARCHAR(MAX)     JSON blob — see Models/Database/JsonFields/OfferAttributes.cs
--                                   Example value:
--                                   {
--                                     "baggageAllowance": "23kg",
--                                     "isRefundable": true,
--                                     "isChangeable": false,
--                                     "seatsRemaining": 4
--                                   }
--   CreatedAt     DATETIME2(7)      UTC; set once on insert
--   UpdatedAt     DATETIME2(7)      UTC; updated on every write
-- =============================================================================

CREATE TABLE [offer].[Offers]
(
    [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Offers_Id]          DEFAULT NEWID(),
    [FlightNumber] NVARCHAR(10)     NOT NULL,
    [Origin]       NVARCHAR(3)      NOT NULL,
    [Destination]  NVARCHAR(3)      NOT NULL,
    [DepartureAt]  DATETIME2(7)     NOT NULL,
    [FareClass]    NVARCHAR(50)     NOT NULL,
    [TotalPrice]   DECIMAL(18, 2)   NOT NULL,
    [Currency]     NVARCHAR(3)      NOT NULL,
    [Status]       NVARCHAR(50)     NOT NULL CONSTRAINT [DF_Offers_Status]      DEFAULT 'available',
    [Attributes]   NVARCHAR(MAX)    NULL,
    [CreatedAt]    DATETIME2(7)     NOT NULL CONSTRAINT [DF_Offers_CreatedAt]   DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]    DATETIME2(7)     NOT NULL CONSTRAINT [DF_Offers_UpdatedAt]   DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_Offers] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CHK_Offers_Status]      CHECK ([Status] IN ('available', 'sold', 'expired')),
    CONSTRAINT [CHK_Offers_FareClass]   CHECK ([FareClass] IN ('economy', 'premium_economy', 'business', 'first')),
    CONSTRAINT [CHK_Offers_TotalPrice]  CHECK ([TotalPrice] >= 0),

    -- Validate the JSON column contains a JSON object (SQL Server 2016+)
    CONSTRAINT [CHK_Offers_Attributes_IsJson] CHECK ([Attributes] IS NULL OR ISJSON([Attributes]) = 1)
);
GO

-- Supports filtering by status (e.g. list all available offers)
CREATE NONCLUSTERED INDEX [IX_Offers_Status]
    ON [offer].[Offers] ([Status] ASC)
    INCLUDE ([FlightNumber], [Origin], [Destination], [DepartureAt], [FareClass], [TotalPrice], [Currency]);
GO

-- Supports flight-level queries (e.g. all offers for a given flight)
CREATE NONCLUSTERED INDEX [IX_Offers_FlightNumber]
    ON [offer].[Offers] ([FlightNumber] ASC)
    INCLUDE ([Status], [FareClass], [DepartureAt]);
GO

-- Supports route searches by origin/destination pair
CREATE NONCLUSTERED INDEX [IX_Offers_Origin_Destination]
    ON [offer].[Offers] ([Origin] ASC, [Destination] ASC)
    INCLUDE ([DepartureAt], [Status], [FareClass], [TotalPrice]);
GO

-- Supports date-range queries and default listing sort (soonest departure first)
CREATE NONCLUSTERED INDEX [IX_Offers_DepartureAt]
    ON [offer].[Offers] ([DepartureAt] ASC)
    INCLUDE ([Status], [FlightNumber]);
GO
