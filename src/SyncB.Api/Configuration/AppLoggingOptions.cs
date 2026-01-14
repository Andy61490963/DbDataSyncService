namespace DbDataSyncService.SyncB.Configuration;

/// <summary>
/// 日誌設定選項，對應 appsettings.json 的 Logging 區段。
/// </summary>
public sealed class AppLoggingOptions
{
    public const string SectionName = "Logging";

    public string ApplicationName { get; set; } = "DbDataSyncService.SyncB";

    public string MinimumLevel { get; set; } = "Information";

    public FileLogOptions File { get; set; } = new();

    public SeqLogOptions Seq { get; set; } = new();
}

public sealed class FileLogOptions
{
    public bool Enabled { get; set; } = true;

    public int RetainDays { get; set; } = 14;

    public int FileSizeLimitMB { get; set; } = 50;
}

public sealed class SeqLogOptions
{
    public bool Enabled { get; set; }

    public string ServerUrl { get; set; } = "http://localhost:5341";

    public string BufferRelativePath { get; set; } = "seq-buffer";

    public int PeriodSeconds { get; set; } = 2;
}
