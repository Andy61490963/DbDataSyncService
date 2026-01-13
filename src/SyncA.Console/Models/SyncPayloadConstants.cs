namespace DbDataSyncService.SyncA.Models;

/// <summary>
/// 同步 Payload 固定字串常數。
/// </summary>
public static class SyncPayloadConstants
{
    /// <summary>
    /// Upsert 操作代碼。
    /// </summary>
    public const string Upsert = "U";

    /// <summary>
    /// Delete 操作代碼。
    /// </summary>
    public const string Delete = "D";
}
