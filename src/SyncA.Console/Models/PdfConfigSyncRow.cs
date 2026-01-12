namespace DbDataSyncService.SyncA.Models;

/// <summary>
/// PDF 設定同步資料列，需與 dbo.PDFConfigSyncServiceConfig 欄位一致。
/// </summary>
public sealed class PdfConfigSyncRow
{
    /// <summary>
    /// 主鍵。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 設定鍵值（範例欄位，請依實際資料表補齊）。
    /// </summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>
    /// 設定內容（範例欄位，請依實際資料表補齊）。
    /// </summary>
    public string? ConfigValue { get; set; }

    /// <summary>
    /// 啟用旗標（範例欄位，請依實際資料表補齊）。
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 最後更新時間（範例欄位，請依實際資料表補齊）。
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
