-- =============================================================================
-- Migration: Add Tax column to seat.SeatPricing and bag.BagPricing (PR #779)
-- Date:      2026-04-18
-- Safe to run multiple times (all statements are idempotent).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Add Tax column to seat.SeatPricing
--
--    Stores the tax amount (20% of Price, auto-computed on Create()).
--    DEFAULT 0.00 so existing rows are valid immediately; back-fill below
--    recalculates to the correct 20% value.
-- -----------------------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID('[seat].[SeatPricing]')
    AND    name      = 'Tax'
)
BEGIN
    ALTER TABLE [seat].[SeatPricing]
        ADD Tax DECIMAL(10,2) NOT NULL
            CONSTRAINT DF_SeatPricing_Tax DEFAULT 0.00;

    PRINT 'Added seat.SeatPricing.Tax';
END
ELSE
    PRINT 'seat.SeatPricing.Tax already exists — skipped';

GO

-- -----------------------------------------------------------------------------
-- 2. Back-fill Tax for existing seat.SeatPricing rows
-- -----------------------------------------------------------------------------

UPDATE [seat].[SeatPricing]
SET    Tax = ROUND(Price * 0.20, 2)
WHERE  Tax = 0.00;

PRINT 'Back-filled seat.SeatPricing.Tax';

GO

-- -----------------------------------------------------------------------------
-- 3. Add Tax column to bag.BagPricing
-- -----------------------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID('[bag].[BagPricing]')
    AND    name      = 'Tax'
)
BEGIN
    ALTER TABLE [bag].[BagPricing]
        ADD Tax DECIMAL(10,2) NOT NULL
            CONSTRAINT DF_BagPricing_Tax DEFAULT 0.00;

    PRINT 'Added bag.BagPricing.Tax';
END
ELSE
    PRINT 'bag.BagPricing.Tax already exists — skipped';

GO

-- -----------------------------------------------------------------------------
-- 4. Back-fill Tax for existing bag.BagPricing rows
-- -----------------------------------------------------------------------------

UPDATE [bag].[BagPricing]
SET    Tax = ROUND(Price * 0.20, 2)
WHERE  Tax = 0.00;

PRINT 'Back-filled bag.BagPricing.Tax';

GO

PRINT 'Migration complete.';
