-- =============================================================================
-- Migration: Ticketing value attribution (PR #778)
-- Target:    delivery schema
-- Date:      2026-04-18
-- Safe to run multiple times (all statements are idempotent).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Safety cleanup — drop intermediate tables if an earlier version of this
--    feature branch was ever deployed. These tables are NOT present in the
--    baseline schema; the DROP is harmless if they don't exist.
-- -----------------------------------------------------------------------------

IF OBJECT_ID('[delivery].[TicketTaxCoupon]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [delivery].[TicketTaxCoupon];
    PRINT 'Dropped delivery.TicketTaxCoupon';
END

IF OBJECT_ID('[delivery].[TicketTax]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [delivery].[TicketTax];
    PRINT 'Dropped delivery.TicketTax';
END

GO

-- -----------------------------------------------------------------------------
-- 2. Add FareCalculation column to delivery.Ticket
--
--    Stores the IATA linear fare calculation string
--    (e.g. "LHR AX JFK 625.00 AX LHR 625.00 NUC1250.00 END ROE1.000000").
--    Used at read time to derive NUC-weighted per-coupon fare shares.
--    Financial data (baseFare, currency, taxes with coupon attribution) lives
--    in the existing TicketData JSON column under fareConstruction.
-- -----------------------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID('[delivery].[Ticket]')
    AND    name      = 'FareCalculation'
)
BEGIN
    ALTER TABLE [delivery].[Ticket]
        ADD FareCalculation NVARCHAR(500) NOT NULL
            CONSTRAINT DF_Ticket_FareCalc DEFAULT '';

    PRINT 'Added delivery.Ticket.FareCalculation';
END
ELSE
    PRINT 'delivery.Ticket.FareCalculation already exists — skipped';

GO

-- -----------------------------------------------------------------------------
-- 3. Back-fill FareCalculation for existing tickets
--
--    Existing tickets have TicketData JSON but no FareCalculation string.
--    We leave the column as '' (empty string); GetCouponValueHandler already
--    handles the case where FareCalculation is absent by returning null
--    (HTTP 404) for the coupon value endpoint.
--
--    If you have access to the original fare calculation strings, replace the
--    UPDATE below with a targeted set per ticket number.
-- -----------------------------------------------------------------------------

-- UPDATE [delivery].[Ticket]
-- SET    FareCalculation = '<IATA calc string>'
-- WHERE  TicketNumber = <number>
--   AND  FareCalculation = '';

PRINT 'Migration complete. Existing tickets have FareCalculation = '''' — ';
PRINT 'coupon value derivation will return 404 for those tickets until';
PRINT 'FareCalculation is populated via a reissue or manual back-fill.';

GO
