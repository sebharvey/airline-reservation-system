-- =============================================================================
-- Schema: template
-- Naming convention: [domain].[Table] — each API owns its own schema.
-- Cross-schema foreign keys are prohibited; integrity is enforced at
-- the application layer per the data principles.
-- =============================================================================

CREATE SCHEMA [template];
GO

-- =============================================================================
-- Table: template.Items
--
-- Columns:
--   Id           UNIQUEIDENTIFIER  PK — application-generated via Guid.NewGuid()
--   Name         NVARCHAR(255)     Required display name
--   Status       NVARCHAR(50)      Enum-like: 'active' | 'inactive'
--   Attributes   NVARCHAR(MAX)     JSON blob — see Models/Database/JsonFields/TemplateItemAttributes.cs
--                                  for the deserialisation target.
--                                  Example value:
--                                  {
--                                    "tags": ["example", "template"],
--                                    "priority": "high",
--                                    "properties": { "source": "api", "version": "1.0" }
--                                  }
--   CreatedAt    DATETIME2(7)      UTC; set once on insert
--   UpdatedAt    DATETIME2(7)      UTC; updated on every write
-- =============================================================================

CREATE TABLE [template].[Items]
(
    [Id]         UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Items_Id]        DEFAULT NEWID(),
    [Name]       NVARCHAR(255)    NOT NULL,
    [Status]     NVARCHAR(50)     NOT NULL CONSTRAINT [DF_Items_Status]    DEFAULT 'active',
    [Attributes] NVARCHAR(MAX)    NULL,
    [CreatedAt]  DATETIME2(7)     NOT NULL CONSTRAINT [DF_Items_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]  DATETIME2(7)     NOT NULL CONSTRAINT [DF_Items_UpdatedAt] DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_Items] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CHK_Items_Status] CHECK ([Status] IN ('active', 'inactive')),

    -- Validate the JSON column contains a JSON object (SQL Server 2016+)
    CONSTRAINT [CHK_Items_Attributes_IsJson] CHECK ([Attributes] IS NULL OR ISJSON([Attributes]) = 1)
);
GO

-- Supports filtering/listing by status
CREATE NONCLUSTERED INDEX [IX_Items_Status]
    ON [template].[Items] ([Status] ASC)
    INCLUDE ([Name], [CreatedAt]);
GO

-- Supports default listing sort (most recent first)
CREATE NONCLUSTERED INDEX [IX_Items_CreatedAt]
    ON [template].[Items] ([CreatedAt] DESC);
GO
