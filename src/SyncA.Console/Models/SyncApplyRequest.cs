namespace DbDataSyncService.SyncA.Models;

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
