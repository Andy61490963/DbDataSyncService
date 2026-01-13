namespace DbDataSyncService.SyncA.Configuration;

public sealed class SyncJobOptions
{
    public const string SectionName = "SyncJob";

    public string TargetApiBaseUrl { get; init; } = string.Empty;

    public string SyncKey { get; init; } = string.Empty;

    public int BatchSize { get; init; } = 2000;

    /// <summary>
    /// 同步間隔（分鐘）
    /// </summary>
    public int IntervalMinutes { get; init; } = 5;
}