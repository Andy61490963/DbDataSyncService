namespace DbDataSyncService.SyncA.Models;

/// <summary>
/// A 端送往 B 端的同步套用要求。
/// </summary>
public sealed class SyncApplyRequest
{
    public string SyncKey { get; set; } = string.Empty;

    public long FromVersion { get; set; }

    public long ToVersion { get; set; }

    public IReadOnlyList<PdfConfigSyncRow> Upserts { get; set; } = Array.Empty<PdfConfigSyncRow>();

    public IReadOnlyList<Guid> Deletes { get; set; } = Array.Empty<Guid>();
}

/// <summary>
/// Change Tracking 操作別。
/// </summary>
public enum ChangeOperation
{
    InsertOrUpdate,
    Delete
}

/// <summary>
/// Change Tracking 變更記錄。
/// </summary>
public sealed class ChangeRow
{
    public Guid Id { get; set; }

    public ChangeOperation Operation { get; set; }
}
