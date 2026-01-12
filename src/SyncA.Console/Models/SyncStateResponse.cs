namespace DbDataSyncService.SyncA.Models;

/// <summary>
/// 代表 B 端回傳的同步水位狀態。
/// </summary>
public sealed class SyncStateResponse
{
    public string SyncKey { get; set; } = string.Empty;

    public long LastVersion { get; set; }

    public DateTime? LastSyncTime { get; set; }

    public string? LastError { get; set; }
}
