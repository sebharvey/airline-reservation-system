-- =============================================================================
-- APEX AIR — IMPORT INTERIM 2026 FLIGHT SCHEDULE
-- =============================================================================
-- Deletes all existing schedule rows and replaces them with the interim 2026
-- schedule defined in res/schedules/flight-schedule-interim-2026.md.
--
-- DaysOfWeek bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64
-- Daily = 127
-- =============================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

-- -----------------------------------------------------------------------------
-- 1. DELETE existing schedule data (child before parent)
-- -----------------------------------------------------------------------------

DELETE FROM [schedule].[FlightSchedule];
DELETE FROM [schedule].[ScheduleGroup];

-- -----------------------------------------------------------------------------
-- 2. INSERT schedule group
-- -----------------------------------------------------------------------------

DECLARE @GroupId UNIQUEIDENTIFIER = NEWID();

INSERT INTO [schedule].[ScheduleGroup]
    (ScheduleGroupId, Name, SeasonStart, SeasonEnd, IsActive, CreatedBy)
VALUES
    (@GroupId, 'Interim 2026', '2026-01-01', '2026-12-31', 1, 'ops-admin@apexair.com');

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
    (@GroupId, 'AX001', 'LHR', 'JFK', '08:00', '11:10', 0, '07:00', '15:10', 0, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX002', 'JFK', 'LHR', '13:00', '01:15', 1, '17:00', '00:15', 1, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX003', 'LHR', 'JFK', '10:30', '13:45', 0, '09:30', '17:45', 0, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX004', 'JFK', 'LHR', '17:30', '05:45', 1, '21:30', '04:45', 1, 127, 'A351', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX005', 'LHR', 'JFK', '13:00', '16:15', 0, '12:00', '20:15', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX006', 'JFK', 'LHR', '20:00', '08:15', 1, '00:00', '07:15', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX007', 'LHR', 'JFK', '16:00', '19:15', 0, '15:00', '23:15', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX008', 'JFK', 'LHR', '22:30', '10:45', 1, '02:30', '09:45', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX009', 'LHR', 'JFK', '19:30', '22:45', 0, '18:30', '02:45', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX010', 'JFK', 'LHR', '00:30', '12:45', 1, '04:30', '11:45', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- North America — LHR ↔ MIA -------------------------------------------------
    (@GroupId, 'AX021', 'LHR', 'MIA', '09:30', '14:00', 0, '08:30', '18:00', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX022', 'MIA', 'LHR', '17:15', '06:00', 1, '21:15', '05:00', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX023', 'LHR', 'MIA', '14:00', '18:30', 0, '13:00', '22:30', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX024', 'MIA', 'LHR', '21:30', '10:15', 1, '01:30', '09:15', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- South Asia — LHR ↔ BOM ----------------------------------------------------
    (@GroupId, 'AX401', 'LHR', 'BOM', '21:00', '09:30', 1, '20:00', '04:00', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX402', 'BOM', 'LHR', '02:30', '07:15', 0, '21:00', '06:15', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- South Asia — LHR ↔ DEL ----------------------------------------------------
    (@GroupId, 'AX411', 'LHR', 'DEL', '20:30', '09:00', 1, '19:30', '03:30', 1, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX412', 'DEL', 'LHR', '03:30', '08:00', 0, '22:00', '07:00', 0, 127, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
-- South Asia — LHR ↔ BLR (Mon/Wed/Fri/Sun and Tue/Thu/Sat/Mon) --------------
    (@GroupId, 'AX421', 'LHR', 'BLR', '22:30', '12:15', 1, '21:30', '06:45', 1,  85, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com'),
    (@GroupId, 'AX422', 'BLR', 'LHR', '02:00', '07:45', 0, '20:30', '06:45', 0,  43, 'B789', '2026-01-01', '2026-12-31', 0, 'ops-admin@apexair.com');

COMMIT TRANSACTION;

PRINT 'Interim 2026 schedule imported: 1 schedule group, 20 flight schedules.';
