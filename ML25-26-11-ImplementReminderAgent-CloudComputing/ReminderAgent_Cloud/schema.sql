-- =============================================================================
-- ReminderAgent Cloud Migration — Azure SQL Database Schema (2-table version)
-- Module: ML25/26-11 | Group: TechTrio
-- =============================================================================
-- Two tables only:
--   1. Assets   — reminder data + country fields + embedding, all inline
--   2. Photos   — photo attachments, FK to Assets
-- Safe to re-run: all objects are guarded with IF NOT EXISTS checks.
-- =============================================================================
GO

-- =============================================================================
-- 1. Assets
--    Single table holding reminder data, location (country/region inlined
--    instead of a separate Countries table), and the embedding vector.
--    - Tags stays a semicolon-delimited NVARCHAR, same format as the CSV.
--    - TimelineState is NOT stored — still computed at query time in C#
--      from EventDate, matching the original design.
--    - Country/Region are plain strings here (no FK) since there's no
--      separate Countries table in this version.
--    - Embedding is VECTOR(1536): a native SQL Server vector column, mapped
--      to SqlVector<float> in C#.
--      Azure SQL cannot do cosine similarity natively, so SimilarityService
--      still loads all vectors into memory and scores them in C#.
-- =============================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Assets' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Assets
    (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        Name            NVARCHAR(255)    NOT NULL,
        Category        NVARCHAR(100)    NULL,
        Tags            NVARCHAR(MAX)    NULL,           -- semicolon-delimited
        TimelineState   NVARCHAR(20)     NULL,           -- optional cache; computed in C# at query time
        EventDate       DATE             NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UserInput       NVARCHAR(MAX)    NULL,
        UserExperience  NVARCHAR(255)    NULL,
        City            NVARCHAR(100)    NULL,
        Country         NVARCHAR(100)    NULL,
        Region          NVARCHAR(100)    NULL,
        Embedding       VECTOR(1536)     NULL,           

        CONSTRAINT PK_Assets PRIMARY KEY CLUSTERED (Id)
    );
END
GO

-- =============================================================================
-- 2. Photos
--    Replaces the PhotoRefs List<string> in Asset.cs with a proper table.
--    BlobName    — filename inside the 'photos/' container (e.g. "abc123.jpg")
--    BlobUrl     — full Azure Blob Storage URL for direct access
--    AccessKey   — SAS token or access key (nullable; generated on upload)
--    FileName    — original filename supplied by the user
--    ContentType — MIME type (e.g. "image/jpeg")
-- =============================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Photos' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Photos
    (
        Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        AssetId     UNIQUEIDENTIFIER NOT NULL,
        BlobName    NVARCHAR(500)    NOT NULL,
        BlobUrl     NVARCHAR(2048)   NOT NULL,
        AccessKey   NVARCHAR(1024)   NULL,
        FileName    NVARCHAR(255)    NULL,
        ContentType NVARCHAR(100)    NULL,
        UploadedAt  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_Photos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Photos_Assets FOREIGN KEY (AssetId)
            REFERENCES dbo.Assets (Id)
            ON DELETE CASCADE     -- remove photos when their asset is deleted
    );
END
GO

-- =============================================================================
-- 3. Indexes
--    Assets(Category) — SearchAssets filters by category frequently
--    Assets(Country)   — location matching queries
--    Photos(AssetId)   — GetAssetDetails loads photos by asset
-- =============================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Assets_Category' AND object_id = OBJECT_ID('dbo.Assets')
)
    CREATE NONCLUSTERED INDEX IX_Assets_Category
        ON dbo.Assets (Category)
        INCLUDE (Name, EventDate, City);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Assets_Country' AND object_id = OBJECT_ID('dbo.Assets')
)
    CREATE NONCLUSTERED INDEX IX_Assets_Country
        ON dbo.Assets (Country)
        INCLUDE (Name, City, Region);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Photos_AssetId' AND object_id = OBJECT_ID('dbo.Photos')
)
    CREATE NONCLUSTERED INDEX IX_Photos_AssetId
        ON dbo.Photos (AssetId)
        INCLUDE (BlobUrl, FileName, ContentType);
GO

-- =============================================================================
-- 4. Smoke-test — verify both tables exist and column counts are correct.
--    Returns one row per table; zero rows = something is missing.
-- =============================================================================
SELECT
    t.name             AS TableName,
    COUNT(c.column_id) AS ColumnCount
FROM sys.tables  t
JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.schema_id = SCHEMA_ID('dbo')
  AND t.name IN ('Assets', 'Photos')
GROUP BY t.name
ORDER BY t.name;
GO
-- Expected output:
--   Assets   13
--   Photos    8
