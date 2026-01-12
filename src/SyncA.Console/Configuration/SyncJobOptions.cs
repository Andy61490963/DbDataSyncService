namespace DbDataSyncService.SyncA.Configuration;

/// <summary>
/// 同步作業設定，描述來源資料庫與遠端 API 相關參數。
/// </summary>
public sealed class SyncJobOptions
{
    public const string SectionName = "SyncJob";

    /// <summary>
    /// B 端 API 基底網址。
    /// </summary>
    public string TargetApiBaseUrl { get; set; } = "https://target-host";

    /// <summary>
    /// 同步識別鍵，對應 ZZ_SYNC_STATE.SyncKey。
    /// </summary>
    public string SyncKey { get; set; } = "dcmatev4.dbo.PDFConfigSyncServiceConfig";

    /// <summary>
    /// 每批次最大筆數，避免單次要求過大。
    /// </summary>
    public int BatchSize { get; set; } = 2000;
}
