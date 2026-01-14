namespace DbDataSyncService.SyncA.Configuration;

/// <summary>
/// 日誌設定選項，對應 appsettings.json 的 Logging 區段。
/// </summary>
public sealed class AppLoggingOptions
{
    public const string SectionName = "Logging";

    /// <summary>
    /// 應用程式名稱，將寫入日誌欄位以利查詢。
    /// </summary>
    public string ApplicationName { get; set; } = "DbDataSyncService.SyncA";

    /// <summary>
    /// 最低日誌等級（Information/Debug/Warning...）。
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// 檔案日誌設定。
    /// </summary>
    public FileLogOptions File { get; set; } = new();

    /// <summary>
    /// Seq 日誌設定。
    /// </summary>
    public SeqLogOptions Seq { get; set; } = new();
}

/// <summary>
/// 檔案日誌細項設定。
/// </summary>
public sealed class FileLogOptions
{
    public bool Enabled { get; set; } = true;

    public int RetainDays { get; set; } = 14;

    public int FileSizeLimitMB { get; set; } = 50;
}

/// <summary>
/// Seq 日誌細項設定。
/// </summary>
public sealed class SeqLogOptions
{
    public bool Enabled { get; set; }

    public string ServerUrl { get; set; } = "http://localhost:5341";

    public string BufferRelativePath { get; set; } = "seq-buffer";

    public int PeriodSeconds { get; set; } = 2;
}
