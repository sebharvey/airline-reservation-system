-- =============================================================================
-- MIGRATION: Remove IdentityReference from identity.UserAccount
--            and add FK from customer.Customer.IdentityId
--            to identity.UserAccount.UserAccountId
-- =============================================================================
-- Run this script against an existing database that already has the
-- IdentityReference column, its default constraint, and its unique constraint.
--
-- BEFORE RUNNING:
--   1. Take a full database backup.
--   2. Ensure customer.Customer.IdentityId values are NULL or already match
--      a UserAccountId in identity.UserAccount (required for the FK).
--   3. Review the data migration step below and adjust if needed.
-- =============================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY

    -- -------------------------------------------------------------------------
    -- STEP 1: Migrate data
    -- customer.Customer.IdentityId currently holds the IdentityReference value
    -- from identity.UserAccount. We need to remap it to UserAccountId.
    -- -------------------------------------------------------------------------
    PRINT 'Step 1: Remapping customer.Customer.IdentityId to UserAccountId...';

    UPDATE c
    SET    c.IdentityId = u.UserAccountId
    FROM   [customer].[Customer]  c
    INNER JOIN [identity].[UserAccount] u
           ON u.IdentityReference = c.IdentityId;

    PRINT CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' customer row(s) updated.';

    -- NULL out any IdentityId values that could not be remapped
    -- (i.e. no matching IdentityReference existed in UserAccount).
    UPDATE [customer].[Customer]
    SET    IdentityId = NULL
    WHERE  IdentityId IS NOT NULL
      AND  IdentityId NOT IN (SELECT UserAccountId FROM [identity].[UserAccount]);

    IF @@ROWCOUNT > 0
        PRINT 'WARNING: ' + CAST(@@ROWCOUNT AS NVARCHAR(10))
              + ' customer row(s) had an unmatchable IdentityId and were set to NULL.';

    -- -------------------------------------------------------------------------
    -- STEP 2: Drop the unique constraint on IdentityReference
    -- -------------------------------------------------------------------------
    PRINT 'Step 2: Dropping UQ_UserAccount_IdRef...';

    IF EXISTS (
        SELECT 1 FROM sys.key_constraints
        WHERE  name = 'UQ_UserAccount_IdRef'
          AND  parent_object_id = OBJECT_ID('[identity].[UserAccount]'))
    BEGIN
        ALTER TABLE [identity].[UserAccount]
            DROP CONSTRAINT UQ_UserAccount_IdRef;
        PRINT '  UQ_UserAccount_IdRef dropped.';
    END
    ELSE
        PRINT '  UQ_UserAccount_IdRef not found — skipping.';

    -- -------------------------------------------------------------------------
    -- STEP 3: Drop the default constraint on IdentityReference
    -- -------------------------------------------------------------------------
    PRINT 'Step 3: Dropping DF_UserAccount_IdRef...';

    IF EXISTS (
        SELECT 1 FROM sys.default_constraints
        WHERE  name = 'DF_UserAccount_IdRef'
          AND  parent_object_id = OBJECT_ID('[identity].[UserAccount]'))
    BEGIN
        ALTER TABLE [identity].[UserAccount]
            DROP CONSTRAINT DF_UserAccount_IdRef;
        PRINT '  DF_UserAccount_IdRef dropped.';
    END
    ELSE
        PRINT '  DF_UserAccount_IdRef not found — skipping.';

    -- -------------------------------------------------------------------------
    -- STEP 4: Drop the IdentityReference column
    -- -------------------------------------------------------------------------
    PRINT 'Step 4: Dropping IdentityReference column from identity.UserAccount...';

    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE  name = 'IdentityReference'
          AND  object_id = OBJECT_ID('[identity].[UserAccount]'))
    BEGIN
        ALTER TABLE [identity].[UserAccount]
            DROP COLUMN IdentityReference;
        PRINT '  IdentityReference column dropped.';
    END
    ELSE
        PRINT '  IdentityReference column not found — skipping.';

    -- -------------------------------------------------------------------------
    -- STEP 5: Add FK from customer.Customer.IdentityId
    --         to identity.UserAccount.UserAccountId
    -- -------------------------------------------------------------------------
    PRINT 'Step 5: Adding FK_Customer_UserAccount...';

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE  name = 'FK_Customer_UserAccount'
          AND  parent_object_id = OBJECT_ID('[customer].[Customer]'))
    BEGIN
        ALTER TABLE [customer].[Customer]
            ADD CONSTRAINT FK_Customer_UserAccount
                FOREIGN KEY (IdentityId) REFERENCES [identity].[UserAccount](UserAccountId);
        PRINT '  FK_Customer_UserAccount added.';
    END
    ELSE
        PRINT '  FK_Customer_UserAccount already exists — skipping.';

    -- -------------------------------------------------------------------------
    COMMIT TRANSACTION;
    PRINT '=== Migration completed successfully. ===';

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrLine INT            = ERROR_LINE();
    DECLARE @ErrProc NVARCHAR(200)  = ISNULL(ERROR_PROCEDURE(), 'N/A');

    PRINT '=== MIGRATION FAILED — transaction rolled back ===';
    PRINT 'Procedure : ' + @ErrProc;
    PRINT 'Line      : ' + CAST(@ErrLine AS NVARCHAR(10));
    PRINT 'Message   : ' + @ErrMsg;

    THROW;

END CATCH;
