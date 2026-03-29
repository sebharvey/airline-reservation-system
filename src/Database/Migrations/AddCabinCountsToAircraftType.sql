-- Migration: Add CabinCounts column to seat.AircraftType
-- Description: Adds a nullable JSON column for per-cabin seat counts
-- Run against an existing database where seat.AircraftType already exists

IF NOT EXISTS (
    SELECT 1
    FROM   INFORMATION_SCHEMA.COLUMNS
    WHERE  TABLE_SCHEMA = 'seat'
      AND  TABLE_NAME   = 'AircraftType'
      AND  COLUMN_NAME  = 'CabinCounts'
)
BEGIN
    ALTER TABLE [seat].[AircraftType]
        ADD CabinCounts NVARCHAR(MAX) NULL;

    ALTER TABLE [seat].[AircraftType]
        ADD CONSTRAINT CHK_CabinCounts CHECK (CabinCounts IS NULL OR ISJSON(CabinCounts) = 1);

    PRINT 'Added CabinCounts column to seat.AircraftType';
END
ELSE
BEGIN
    PRINT 'CabinCounts column already exists on seat.AircraftType — skipping';
END
GO
