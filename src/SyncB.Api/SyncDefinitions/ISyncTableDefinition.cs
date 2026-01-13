using System.Data;
using DbDataSyncService.SyncB.Models;

namespace DbDataSyncService.SyncB.SyncDefinitions;

/// <summary>
/// 同步資料表定義（保留多表擴充點）。
/// </summary>
public interface ISyncTableDefinition
{
    /// <summary>
    /// 完整資料表名稱（schema.table）。
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// 主鍵欄位名稱。
    /// </summary>
    string PrimaryKey { get; }

    /// <summary>
    /// Upsert 的 Stored Procedure 名稱。
    /// </summary>
    string UpsertProcedure { get; }

    /// <summary>
    /// Delete 的 Stored Procedure 名稱。
    /// </summary>
    string DeleteProcedure { get; }

    /// <summary>
    /// Upsert 使用的 TVP 型別名稱。
    /// </summary>
    string UpsertTvpTypeName { get; }

    /// <summary>
    /// Delete 使用的 TVP 型別名稱。
    /// </summary>
    string DeleteTvpTypeName { get; }

    /// <summary>
    /// 建立 Upsert 用的資料表結構。
    /// </summary>
    DataTable CreateUpsertTable();

    /// <summary>
    /// 建立 Delete 用的資料表結構。
    /// </summary>
    DataTable CreateDeleteTable();

    /// <summary>
    /// 將 Sync payload 轉成 TVP 資料列並加入資料表。
    /// </summary>
    int AddRows(SyncTablePayload payload, DataTable upserts, DataTable deletes);
}
