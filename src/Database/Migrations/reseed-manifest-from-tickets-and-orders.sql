-- =============================================================================
-- Migration: delivery.Ticket.PassengerId  VARCHAR(20) → INT
-- =============================================================================
-- Converts the composite string passenger identifier (e.g. "PAX-1") to a plain
-- integer by extracting the numeric suffix after the last hyphen.
-- Rows where PassengerId cannot be parsed are surfaced and block the migration.
--
-- Safe to re-run: each step is guarded by a sys.columns type check.
-- Run inside a transaction so a failed parse rolls back cleanly.
-- =============================================================================

SET NOCOUNT ON;
GO

BEGIN TRANSACTION;
BEGIN TRY

    -- -------------------------------------------------------------------------
    -- Step 1: nothing to do if column is already INT
    -- -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1
        FROM   sys.columns c
        JOIN   sys.types   t ON t.user_type_id = c.user_type_id
        WHERE  c.object_id = OBJECT_ID('[delivery].[Ticket]')
          AND  c.name      = 'PassengerId'
          AND  t.name      = 'int'
    )
    BEGIN
        PRINT 'PassengerId is already INT — nothing to do.';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- -------------------------------------------------------------------------
    -- Step 2: verify the column is the expected VARCHAR type before proceeding
    -- -------------------------------------------------------------------------
    IF NOT EXISTS (
        SELECT 1
        FROM   sys.columns c
        JOIN   sys.types   t ON t.user_type_id = c.user_type_id
        WHERE  c.object_id = OBJECT_ID('[delivery].[Ticket]')
          AND  c.name      = 'PassengerId'
          AND  t.name      IN ('varchar', 'nvarchar', 'char', 'nchar')
    )
    BEGIN
        RAISERROR('PassengerId column has an unexpected type — migration aborted.', 16, 1);
    END

    -- -------------------------------------------------------------------------
    -- Step 3: add a staging column to hold the converted integers
    -- -------------------------------------------------------------------------
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE  object_id = OBJECT_ID('[delivery].[Ticket]')
          AND  name      = 'PassengerIdInt'
    )
    BEGIN
        ALTER TABLE [delivery].[Ticket] ADD PassengerIdInt INT NULL;
        PRINT 'Added staging column PassengerIdInt.';
    END

    -- -------------------------------------------------------------------------
    -- Step 4: migrate data
    --   "PAX-1"  → 1      (extract suffix after last hyphen)
    --   "1"      → 1      (already numeric, no hyphen)
    --   unparseable → NULL (TRY_CAST absorbs bad values; surfaced and rejected below)
    -- -------------------------------------------------------------------------
    UPDATE [delivery].[Ticket]
    SET    PassengerIdInt =
               CASE
                   WHEN CHARINDEX('-', PassengerId) > 0 THEN
                       TRY_CAST(
                           REVERSE(LEFT(REVERSE(PassengerId),
                                        CHARINDEX('-', REVERSE(PassengerId)) - 1))
                           AS INT)
                   ELSE
                       TRY_CAST(PassengerId AS INT)
               END;

    DECLARE @Migrated INT = @@ROWCOUNT;
    DECLARE @Nulled   INT = (SELECT COUNT(*) FROM [delivery].[Ticket]
                             WHERE PassengerIdInt IS NULL);

    PRINT CONCAT('Rows updated: ', @Migrated,
                 ' — rows where parse failed: ', @Nulled);

    IF @Nulled > 0
    BEGIN
        SELECT TicketId, PassengerId AS UnparseableValue
        FROM   [delivery].[Ticket]
        WHERE  PassengerIdInt IS NULL;

        RAISERROR('%d row(s) could not be parsed — see result set above. Migration aborted.', 16, 1, @Nulled);
    END

    -- -------------------------------------------------------------------------
    -- Step 5: drop the old VARCHAR column
    -- -------------------------------------------------------------------------
    ALTER TABLE [delivery].[Ticket] DROP COLUMN PassengerId;
    PRINT 'Dropped old PassengerId VARCHAR column.';

    -- -------------------------------------------------------------------------
    -- Step 6: rename the staging column to PassengerId
    -- -------------------------------------------------------------------------
    EXEC sp_rename 'delivery.Ticket.PassengerIdInt', 'PassengerId', 'COLUMN';
    PRINT 'Renamed PassengerIdInt → PassengerId.';

    COMMIT TRANSACTION;
    PRINT 'Migration completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @Msg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @Sev  INT            = ERROR_SEVERITY();
    DECLARE @St   INT            = ERROR_STATE();
    RAISERROR(@Msg, @Sev, @St);
END CATCH
GO
