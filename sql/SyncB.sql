/* === B 端 SQL 腳本：TVP + SP + ZZ_SYNC_STATE === */

IF TYPE_ID(N'dbo.GuidList') IS NULL
BEGIN
    CREATE TYPE dbo.GuidList AS TABLE
    (
        Id uniqueidentifier NOT NULL
    );
END;
GO

IF TYPE_ID(N'dbo.PDFConfigSyncServiceConfigTvp') IS NULL
BEGIN
    CREATE TYPE dbo.PDFConfigSyncServiceConfigTvp AS TABLE
    (
        Id uniqueidentifier NOT NULL,
        ConfigKey nvarchar(200) NOT NULL,
        ConfigValue nvarchar(max) NULL,
        IsEnabled bit NOT NULL,
        UpdatedAt datetime2 NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.ZZ_SYNC_STATE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ZZ_SYNC_STATE
    (
        SyncKey nvarchar(200) NOT NULL PRIMARY KEY,
        LastVersion bigint NOT NULL,
        LastSyncTime datetime NULL,
        LastRowCount int NULL,
        LastError nvarchar(4000) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.usp_PDFConfigSync_Upsert', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.usp_PDFConfigSync_Upsert;
END;
GO

CREATE PROCEDURE dbo.usp_PDFConfigSync_Upsert
    @Rows dbo.PDFConfigSyncServiceConfigTvp READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.PDFConfigSyncServiceConfig AS TARGET
    USING @Rows AS SOURCE
        ON TARGET.ID = SOURCE.Id
    WHEN MATCHED THEN
        UPDATE SET
            TARGET.ConfigKey = SOURCE.ConfigKey,
            TARGET.ConfigValue = SOURCE.ConfigValue,
            TARGET.IsEnabled = SOURCE.IsEnabled,
            TARGET.UpdatedAt = SOURCE.UpdatedAt
    WHEN NOT MATCHED THEN
        INSERT (ID, ConfigKey, ConfigValue, IsEnabled, UpdatedAt)
        VALUES (SOURCE.Id, SOURCE.ConfigKey, SOURCE.ConfigValue, SOURCE.IsEnabled, SOURCE.UpdatedAt);
END;
GO

IF OBJECT_ID(N'dbo.usp_PDFConfigSync_Delete', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.usp_PDFConfigSync_Delete;
END;
GO

CREATE PROCEDURE dbo.usp_PDFConfigSync_Delete
    @Ids dbo.GuidList READONLY
AS
BEGIN
    SET NOCOUNT ON;

    DELETE TARGET
    FROM dbo.PDFConfigSyncServiceConfig AS TARGET
    INNER JOIN @Ids AS IDS
        ON TARGET.ID = IDS.Id;
END;
GO

/* 初始化同步水位 */
IF NOT EXISTS (
    SELECT 1
    FROM dbo.ZZ_SYNC_STATE
    WHERE SyncKey = N'dcmatev4.dbo.PDFConfigSyncServiceConfig'
)
BEGIN
    INSERT INTO dbo.ZZ_SYNC_STATE (SyncKey, LastVersion)
    VALUES (N'dcmatev4.dbo.PDFConfigSyncServiceConfig', 0);
END;
GO
