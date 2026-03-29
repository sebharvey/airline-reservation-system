-- ============================================================================
-- Default Fare Rules — Apex Air (GBP)
-- Blanket rules: no FlightNumber, no ValidFrom/ValidTo
-- Two rule sets: Money and Points for each cabin × fare family
-- Min/Max range allows dynamic pricing based on seat availability
-- ============================================================================

-- Clean existing fare rules
DELETE FROM [offer].[FareRule];

-- ── MONEY RULES ─────────────────────────────────────────────────────────────

INSERT INTO [offer].[FareRule]
    (RuleType, FlightNumber, FareBasisCode, FareFamily, CabinCode, BookingClass,
     CurrencyCode, MinAmount, MaxAmount, TaxAmount,
     MinPoints, MaxPoints, PointsTaxes,
     IsRefundable, IsChangeable, ChangeFeeAmount, CancellationFeeAmount,
     ValidFrom, ValidTo)
VALUES

-- First Flex — fully flexible, refundable, changeable
('Money', NULL, 'FFLEXGB', 'First Flex',  'F', 'F', 'GBP',
 2450.00, 3250.00, 195.00,
 NULL, NULL, NULL,
 1, 1, 0.00, 0.00, NULL, NULL),

-- First Saver — non-refundable, changeable with fee
('Money', NULL, 'FSAVGB', 'First Saver', 'F', 'A', 'GBP',
 1650.00, 2250.00, 195.00,
 NULL, NULL, NULL,
 0, 1, 150.00, 1950.00, NULL, NULL),

-- Business Flex — fully flexible, refundable, changeable
('Money', NULL, 'JFLEXGB', 'Business Flex',  'J', 'J', 'GBP',
 999.00, 1499.00, 182.50,
 NULL, NULL, NULL,
 1, 1, 0.00, 0.00, NULL, NULL),

-- Business Saver — non-refundable, changeable with fee
('Money', NULL, 'JSAVGB', 'Business Saver', 'J', 'C', 'GBP',
 599.00, 999.00, 182.50,
 NULL, NULL, NULL,
 0, 1, 125.00, 799.00, NULL, NULL),

-- Premium Flex — fully flexible, refundable, changeable
('Money', NULL, 'WFLEXGB', 'Premium Flex',  'W', 'W', 'GBP',
 499.00, 799.00, 135.00,
 NULL, NULL, NULL,
 1, 1, 0.00, 0.00, NULL, NULL),

-- Premium Saver — non-refundable, changeable with fee
('Money', NULL, 'WSAVGB', 'Premium Saver', 'W', 'S', 'GBP',
 299.00, 549.00, 135.00,
 NULL, NULL, NULL,
 0, 1, 100.00, 425.00, NULL, NULL),

-- Economy Flex — fully flexible, refundable, changeable
('Money', NULL, 'YFLEXGB', 'Economy Flex',  'Y', 'Y', 'GBP',
 249.00, 449.00, 97.25,
 NULL, NULL, NULL,
 1, 1, 0.00, 0.00, NULL, NULL),

-- Economy Light — non-refundable, non-changeable
('Money', NULL, 'YLOWGB', 'Economy Light', 'Y', 'L', 'GBP',
 89.00, 249.00, 97.25,
 NULL, NULL, NULL,
 0, 0, 0.00, 149.00, NULL, NULL),

-- ── POINTS RULES ────────────────────────────────────────────────────────────

-- First Flex — points redemption
('Points', NULL, 'FFLEXGB', 'First Flex',  'F', 'F', NULL,
 NULL, NULL, NULL,
 200000, 325000, 195.00,
 1, 1, 0.00, 0.00, NULL, NULL),

-- First Saver — points redemption
('Points', NULL, 'FSAVGB', 'First Saver', 'F', 'A', NULL,
 NULL, NULL, NULL,
 140000, 225000, 195.00,
 0, 1, 150.00, 1950.00, NULL, NULL),

-- Business Flex — points redemption
('Points', NULL, 'JFLEXGB', 'Business Flex',  'J', 'J', NULL,
 NULL, NULL, NULL,
 80000, 150000, 182.50,
 1, 1, 0.00, 0.00, NULL, NULL),

-- Business Saver — points redemption
('Points', NULL, 'JSAVGB', 'Business Saver', 'J', 'C', NULL,
 NULL, NULL, NULL,
 50000, 100000, 182.50,
 0, 1, 125.00, 799.00, NULL, NULL),

-- Premium Flex — points redemption
('Points', NULL, 'WFLEXGB', 'Premium Flex',  'W', 'W', NULL,
 NULL, NULL, NULL,
 40000, 80000, 135.00,
 1, 1, 0.00, 0.00, NULL, NULL),

-- Premium Saver — points redemption
('Points', NULL, 'WSAVGB', 'Premium Saver', 'W', 'S', NULL,
 NULL, NULL, NULL,
 25000, 55000, 135.00,
 0, 1, 100.00, 425.00, NULL, NULL),

-- Economy Flex — points redemption
('Points', NULL, 'YFLEXGB', 'Economy Flex',  'Y', 'Y', NULL,
 NULL, NULL, NULL,
 20000, 45000, 97.25,
 1, 1, 0.00, 0.00, NULL, NULL),

-- Economy Light — points redemption
('Points', NULL, 'YLOWGB', 'Economy Light', 'Y', 'L', NULL,
 NULL, NULL, NULL,
 7500, 25000, 97.25,
 0, 0, 0.00, 149.00, NULL, NULL);
