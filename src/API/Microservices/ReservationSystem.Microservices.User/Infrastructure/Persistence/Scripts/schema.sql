-- ============================================================
-- User microservice schema
-- Owns the [user] schema: employee user accounts for the
-- Apex Air reservation system.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'user')
BEGIN
    EXEC('CREATE SCHEMA [user]');
END
GO

-- ============================================================
-- [user].[User]
-- One row per Apex Air employee who can log into the
-- reservation system. Credentials are stored as SHA-256
-- hashes; plain-text passwords are never persisted.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[user].[User]') AND type = 'U'
)
BEGIN
    CREATE TABLE [user].[User] (
        UserId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_User_UserId              DEFAULT NEWID(),
        Username            VARCHAR(100)     NOT NULL,
        Email               VARCHAR(254)     NOT NULL,
        PasswordHash        VARCHAR(255)     NOT NULL,
        FirstName           NVARCHAR(100)    NOT NULL,
        LastName            NVARCHAR(100)    NOT NULL,
        IsActive            BIT              NOT NULL CONSTRAINT DF_User_IsActive            DEFAULT 1,
        IsLocked            BIT              NOT NULL CONSTRAINT DF_User_IsLocked            DEFAULT 0,
        FailedLoginAttempts TINYINT          NOT NULL CONSTRAINT DF_User_FailedLoginAttempts DEFAULT 0,
        LastLoginAt         DATETIME2        NULL,
        CreatedAt           DATETIME2        NOT NULL CONSTRAINT DF_User_CreatedAt           DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2        NOT NULL CONSTRAINT DF_User_UpdatedAt           DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_User          PRIMARY KEY (UserId),
        CONSTRAINT UQ_User_Username UNIQUE      (Username),
        CONSTRAINT UQ_User_Email    UNIQUE      (Email)
    );
END
GO

-- Index: fast lookup by username (login path)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_Username' AND object_id = OBJECT_ID('[user].[User]'))
    CREATE INDEX IX_User_Username ON [user].[User] (Username);
GO

-- Index: fast lookup by email
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_Email' AND object_id = OBJECT_ID('[user].[User]'))
    CREATE INDEX IX_User_Email ON [user].[User] (Email);
GO

-- Trigger: maintain UpdatedAt automatically on every UPDATE
IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_User_UpdatedAt')
BEGIN
    EXEC('
    CREATE TRIGGER TR_User_UpdatedAt
    ON [user].[User]
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE [user].[User]
        SET    UpdatedAt = SYSUTCDATETIME()
        FROM   [user].[User] u
        INNER JOIN inserted i ON u.UserId = i.UserId;
    END
    ');
END
GO
