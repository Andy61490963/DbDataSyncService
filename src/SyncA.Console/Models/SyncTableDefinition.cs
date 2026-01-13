namespace DbDataSyncService.SyncA.Models;

/// <summary>
/// 同步目標資料表定義（保留多表擴充點）。
/// </summary>
public interface ISyncTableDefinition
{
    /// <summary>
    /// 完整資料表名稱（schema.table）。
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// 主鍵欄位名稱。
    /// </summary>
    string PrimaryKey { get; }
}

/// <summary>
/// PDF 設定同步目標表定義。
/// </summary>
public sealed class PdfConfigSyncTableDefinition : ISyncTableDefinition
{
    /// <inheritdoc />
    public string TableName => "dbo.PDFConfigSyncServiceConfig";

    /// <inheritdoc />
    public string PrimaryKey => "ID";
}
