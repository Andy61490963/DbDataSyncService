namespace DbDataSyncService.SyncB.Models;

/// <summary>
/// 同步水位資料傳輸物件。
/// </summary>
public sealed class SyncStateDto
{
    public string SyncKey { get; set; } = string.Empty;

    public long LastVersion { get; set; }

    public DateTime? LastSyncTime { get; set; }

    public int? LastRowCount { get; set; }

    public string? LastError { get; set; }
}
