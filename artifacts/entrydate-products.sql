/*
  Run on: Server=Revan; Database=StockManagmentDb
  This script adds only dbo.Products.EntryDate if missing. It preserves existing
  rows and assigns varied UTC timestamps within the previous six months, matching
  the existing EF migration behavior. The migration history row is written only
  when this script itself added the column and the migration exists in this repo.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @EntryDateAdded bit = 0;
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
    THROW 50001, 'dbo.Products does not exist in the selected database.', 1;

IF COL_LENGTH(N'dbo.Products', N'EntryDate') IS NULL
BEGIN
    ALTER TABLE dbo.Products
        ADD EntryDate datetime2 NOT NULL
            CONSTRAINT DF_Products_EntryDate DEFAULT SYSUTCDATETIME();

    UPDATE dbo.Products
    SET EntryDate = DATEADD(SECOND, -ABS(CONVERT(bigint, CHECKSUM(NEWID())) % 15552000), SYSUTCDATETIME());

    SET @EntryDateAdded = 1;
END;

IF @EntryDateAdded = 1
   AND OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 50002, 'dbo.__EFMigrationsHistory does not exist; apply the migration using EF tooling after confirming the database.', 1;

IF @EntryDateAdded = 1
   AND OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20261016150000_AddProductEntryDate'
   )
BEGIN
    DECLARE @ProductVersion nvarchar(32);
    SELECT TOP (1) @ProductVersion = ProductVersion
    FROM dbo.__EFMigrationsHistory
    ORDER BY MigrationId DESC;

    IF @ProductVersion IS NULL
        THROW 50003, 'Cannot derive ProductVersion from dbo.__EFMigrationsHistory. Apply using EF tooling or inspect migration history first.', 1;

    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261016150000_AddProductEntryDate', @ProductVersion);
END;

COMMIT TRANSACTION;


