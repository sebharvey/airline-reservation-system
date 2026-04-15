-- =============================================================================
-- APEX AIR — IMPORT TEST FLIGHT SCHEDULE
-- =============================================================================
-- Imports the test schedule defined in res/schedules/Test/test-flight-schedule-interim-2026.md.
--
-- If a ScheduleGroup named 'Test 2026' already exists, all FlightSchedule rows
-- belonging to it are deleted before the group is recreated. No other schedule
-- groups are touched.
--
-- DaysOfWeek bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64
-- Daily = 127
--
-- Flights:  3x LHR-JFK (A351 ×2, B789 ×1)
--           1x LHR-MIA (B789)
--           1x LHR-DEL (B789)
-- =============================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

-- -----------------------------------------------------------------------------
-- 1. DELETE existing rows for 'Test 2026' schedule group (if it exists)
-- -----------------------------------------------------------------------------

DECLARE @ExistingGroupId UNIQUEIDENTIFIER;

SELECT @ExistingGroupId = ScheduleGroupId
FROM   [schedule].[ScheduleGroup]
WHERE  Name = 'Test 2026';

IF @ExistingGroupId IS NOT NULL
BEGIN
    DELETE FROM [schedule].[FlightSchedule]
    WHERE  ScheduleGroupId = @ExistingGroupId;

    DELETE FROM [schedule].[ScheduleGroup]
    WHERE  ScheduleGroupId = @ExistingGroupId;
END;

-- -----------------------------------------------------------------------------
-- 2. INSERT schedule group
-- -----------------------------------------------------------------------------

DECLARE @GroupId UNIQUEIDENTIFIER = NEWID();

INSERT INTO [schedule].[ScheduleGroup]
    (ScheduleGroupId, Name, SeasonStart, SeasonEnd, IsActive, CreatedBy)
VALUES
    (@GroupId, 'Test 2026', '2026-01-01', '2026-12-31', 1, 'ops-admin@apexair.com');

-- -----------------------------------------------------------------------------
-- 3. INSERT flight schedules
-- -----------------------------------------------------------------------------

INSERT INTO [schedule].[FlightSchedule]
    (ScheduleGroupId, FlightNumber, Origin, Destination,
     DepartureTime, ArrivalTime, ArrivalDayOffset,
     DepartureTimeUtc, ArrivalTimeUtc, ArrivalDayOffsetUtc,
     DaysOfWeek, AircraftType, ValidFrom, ValidTo, FlightsCreated, CreatedBy)
VALUES
-- North America — LHR ↔ JFK -------------------------------------------------
-- AX001/AX002: Morning service (A351) — LHR dep 09:00, 8h westbound / 7h eastbound
    (@GroupId, 'AX001', 'LHR', 'JFK', '09:00', '12:00', 0, '08:00', '16:00', 0, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX002', 'JFK', 'LHR', '13:30', '01:30', 1, '17:30', '00:30', 1, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- AX003/AX004: Afternoon service (A351) — LHR dep 13:00
    (@GroupId, 'AX003', 'LHR', 'JFK', '13:00', '16:00', 0, '12:00', '20:00', 0, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX004', 'JFK', 'LHR', '17:00', '05:00', 1, '21:00', '04:00', 1, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- AX005/AX006: Evening service (B789) — LHR dep 18:00
    (@GroupId, 'AX005', 'LHR', 'JFK', '18:00', '21:00', 0, '17:00', '01:00', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX006', 'JFK', 'LHR', '19:00', '07:00', 1, '23:00', '06:00', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- North America — LHR ↔ MIA -------------------------------------------------
-- AX021/AX022: Daily service (B789) — 9h30m westbound / 9h eastbound
    (@GroupId, 'AX021', 'LHR', 'MIA', '11:00', '15:30', 0, '10:00', '19:30', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX022', 'MIA', 'LHR', '18:00', '08:00', 1, '22:00', '07:00', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- South Asia — LHR ↔ DEL ----------------------------------------------------
-- AX411/AX412: Daily service (B789) — 8h45m outbound / 9h inbound
    (@GroupId, 'AX411', 'LHR', 'DEL', '21:30', '10:45', 1, '20:30', '05:15', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX412', 'DEL', 'LHR', '14:00', '18:30', 0, '08:30', '17:30', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com');

COMMIT TRANSACTION;

PRINT 'Test 2026 schedule imported: 1 schedule group, 10 flight schedules.';
