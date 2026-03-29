-- ============================================================================
-- Default Fare Rules — Apex Air (GBP)
-- Blanket rules: no FlightNumber, no ValidFrom/ValidTo
-- Covers all 4 cabins (F, J, W, Y) × 2 fare families (Flex, Non-Flex)
-- Points pricing: 100 points per £1 of base fare (Flex only)
-- ============================================================================

INSERT INTO [offer].[FareRule]
    (FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
     CurrencyCode, BaseFareAmount, TaxAmount, TotalAmount,
     IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
     PointsPrice, PointsTaxes, ValidFrom, ValidTo)
VALUES

-- ── First (F) ───────────────────────────────────────────────────────────────

-- First Flex — fully flexible, refundable, changeable, points-redeemable
(NULL, 'FFLEXGB', 'First Flex',  'F', 'F', 'GBP',
 2850.00, 195.00, 3045.00,
 1, 1, 0.00, 0.00,
 285000, 195.00, NULL, NULL),

-- First Saver — non-refundable, changeable with fee, no points redemption
(NULL, 'FSAVGB', 'First Saver', 'F', 'A', 'GBP',
 1950.00, 195.00, 2145.00,
 0, 1, 150.00, 1950.00,
 NULL, NULL, NULL, NULL),

-- ── Business (J) ────────────────────────────────────────────────────────────

-- Business Flex — fully flexible, refundable, changeable, points-redeemable
(NULL, 'JFLEXGB', 'Business Flex',  'J', 'J', 'GBP',
 1250.00, 182.50, 1432.50,
 1, 1, 0.00, 0.00,
 125000, 182.50, NULL, NULL),

-- Business Saver — non-refundable, changeable with fee, no points redemption
(NULL, 'JSAVGB', 'Business Saver', 'J', 'C', 'GBP',
 799.00, 182.50, 981.50,
 0, 1, 125.00, 799.00,
 NULL, NULL, NULL, NULL),

-- ── Premium Economy (W) ─────────────────────────────────────────────────────

-- Premium Flex — fully flexible, refundable, changeable, points-redeemable
(NULL, 'WFLEXGB', 'Premium Flex',  'W', 'W', 'GBP',
 650.00, 135.00, 785.00,
 1, 1, 0.00, 0.00,
 65000, 135.00, NULL, NULL),

-- Premium Saver — non-refundable, changeable with fee, no points redemption
(NULL, 'WSAVGB', 'Premium Saver', 'W', 'S', 'GBP',
 425.00, 135.00, 560.00,
 0, 1, 100.00, 425.00,
 NULL, NULL, NULL, NULL),

-- ── Economy (Y) ─────────────────────────────────────────────────────────────

-- Economy Flex — fully flexible, refundable, changeable, points-redeemable
(NULL, 'YFLEXGB', 'Economy Flex',  'Y', 'Y', 'GBP',
 350.00, 97.25, 447.25,
 1, 1, 0.00, 0.00,
 35000, 97.25, NULL, NULL),

-- Economy Light — non-refundable, non-changeable, cancellation fee = full fare, no points
(NULL, 'YLOWGB', 'Economy Light', 'Y', 'L', 'GBP',
 149.00, 97.25, 246.25,
 0, 0, 0.00, 149.00,
 NULL, NULL, NULL, NULL);
