namespace DbDataSyncService.SyncB.Models;

/// <summary>
/// B 端同步套用請求。
/// </summary>
public sealed class SyncApplyRequest
{
    public string SyncKey { get; set; } = string.Empty;

    public long FromVersion { get; set; }

    public long ToVersion { get; set; }

    public IReadOnlyList<PdfConfigSyncRow> Upserts { get; set; } = Array.Empty<PdfConfigSyncRow>();

    public IReadOnlyList<Guid> Deletes { get; set; } = Array.Empty<Guid>();
}
