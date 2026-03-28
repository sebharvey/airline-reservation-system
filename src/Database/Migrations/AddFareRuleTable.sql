-- Migration: Add offer.FareRule table
-- Run this on an existing database to create the FareRule table, indexes, and triggers.

IF OBJECT_ID('[offer].[FareRule]', 'U') IS NULL
CREATE TABLE [offer].[FareRule] (
    FareRuleId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FareRule_Id          DEFAULT NEWID(),
    FlightNumber          VARCHAR(10)          NULL,
    FareBasisCode         VARCHAR(20)      NOT NULL,
    FareFamily            VARCHAR(50)          NULL,
    CabinCode             CHAR(1)          NOT NULL,
    BookingClass          CHAR(1)          NOT NULL,
    CurrencyCode          CHAR(3)          NOT NULL CONSTRAINT DF_FareRule_Currency    DEFAULT 'GBP',
    BaseFareAmount        DECIMAL(10,2)    NOT NULL,
    TaxAmount             DECIMAL(10,2)    NOT NULL,
    TotalAmount           DECIMAL(10,2)    NOT NULL,
    IsRefundable          BIT              NOT NULL CONSTRAINT DF_FareRule_Refundable  DEFAULT 0,
    IsChangeable          BIT              NOT NULL CONSTRAINT DF_FareRule_Changeable  DEFAULT 0,
    ChangeFeeAmount       DECIMAL(10,2)    NOT NULL CONSTRAINT DF_FareRule_ChangeFee   DEFAULT 0.00,
    CancellationFeeAmount DECIMAL(10,2)    NOT NULL CONSTRAINT DF_FareRule_CancelFee   DEFAULT 0.00,
    PointsPrice           INT                  NULL,
    PointsTaxes           DECIMAL(10,2)        NULL,
    ValidFrom             DATETIME2            NULL,
    ValidTo               DATETIME2            NULL,
    CreatedAt             DATETIME2        NOT NULL CONSTRAINT DF_FareRule_Created     DEFAULT SYSUTCDATETIME(),
    UpdatedAt             DATETIME2        NOT NULL CONSTRAINT DF_FareRule_Updated     DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_FareRule        PRIMARY KEY (FareRuleId),
    CONSTRAINT CHK_FareRule_Cabin CHECK (CabinCode IN ('F','J','W','Y'))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FareRule_FareBasisCode' AND object_id = OBJECT_ID('[offer].[FareRule]'))
    CREATE INDEX IX_FareRule_FareBasisCode ON [offer].[FareRule] (FareBasisCode);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FareRule_FlightNumber' AND object_id = OBJECT_ID('[offer].[FareRule]'))
    CREATE INDEX IX_FareRule_FlightNumber ON [offer].[FareRule] (FlightNumber) WHERE FlightNumber IS NOT NULL;
GO

IF OBJECT_ID('[offer].[TR_FareRule_UpdatedAt]', 'TR') IS NULL
BEGIN
    EXEC('
        CREATE TRIGGER [offer].[TR_FareRule_UpdatedAt]
        ON [offer].[FareRule]
        AFTER UPDATE AS
            UPDATE [offer].[FareRule]
            SET    UpdatedAt = SYSUTCDATETIME()
            FROM   [offer].[FareRule] t
            INNER JOIN inserted i ON t.FareRuleId = i.FareRuleId;
    ');
END
GO
