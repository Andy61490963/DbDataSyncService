/* === A 端 SQL 腳本：GUID TVP（供查詢用） === */

IF TYPE_ID(N'dbo.GuidList') IS NULL
BEGIN
    CREATE TYPE dbo.GuidList AS TABLE
    (
        Id uniqueidentifier NOT NULL
    );
END;
GO
