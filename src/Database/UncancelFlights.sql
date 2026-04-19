-- UncancelFlights.sql
-- Restores cancelled flights back to 'Active' status.
--
-- Usage:
--   1. Run with no parameters to preview affected rows (SELECT only).
--   2. Set @DryRun = 0 and optionally scope by @FlightNumber / @FromDate / @ToDate
--      to perform the update.
--
-- Parameters:
--   @DryRun       1 = preview only (default), 0 = apply update
--   @FlightNumber Flight number to target (NULL = all cancelled flights)
--   @FromDate     Inclusive lower bound on DepartureDate (NULL = no lower bound)
--   @ToDate       Inclusive upper bound on DepartureDate (NULL = no upper bound)

DECLARE @DryRun       BIT       = 1;
DECLARE @FlightNumber VARCHAR(10) = NULL;   -- e.g. 'AX001'
DECLARE @FromDate     DATE        = NULL;   -- e.g. '2026-01-01'
DECLARE @ToDate       DATE        = NULL;   -- e.g. '2026-12-31'

-- ── Preview ──────────────────────────────────────────────────────────────────

SELECT
    InventoryId,
    FlightNumber,
    DepartureDate,
    DepartureTime,
    Origin,
    Destination,
    Status,
    UpdatedAt
FROM [offer].[FlightInventory]
WHERE Status = 'Cancelled'
  AND (@FlightNumber IS NULL OR FlightNumber = @FlightNumber)
  AND (@FromDate     IS NULL OR DepartureDate >= @FromDate)
  AND (@ToDate       IS NULL OR DepartureDate <= @ToDate)
ORDER BY FlightNumber, DepartureDate;

-- ── Apply (skipped when @DryRun = 1) ─────────────────────────────────────────

IF @DryRun = 0
BEGIN
    UPDATE [offer].[FlightInventory]
    SET    Status = 'Active'
    WHERE  Status = 'Cancelled'
      AND  (@FlightNumber IS NULL OR FlightNumber = @FlightNumber)
      AND  (@FromDate     IS NULL OR DepartureDate >= @FromDate)
      AND  (@ToDate       IS NULL OR DepartureDate <= @ToDate);

    PRINT CONCAT(@@ROWCOUNT, ' flight(s) restored to Active.');
END
ELSE
BEGIN
    PRINT 'Dry run — no rows updated. Set @DryRun = 0 to apply.';
END
