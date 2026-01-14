using System.Text.Json.Serialization;

namespace DbDataSyncService.SyncB.Models;

/// <summary>
/// B 端同步套用請求。
/// </summary>
public sealed class SyncApplyJsonRequest
{
    [JsonPropertyName("syncKey")]
    public string SyncKey { get; set; } = string.Empty;

    [JsonPropertyName("fromVersion")]
    public long FromVersion { get; set; }

    [JsonPropertyName("toVersion")]
    public long ToVersion { get; set; }

    [JsonPropertyName("tables")]
    public List<SyncTablePayload> Tables { get; set; } = new();
}

public sealed class SyncTablePayload
{
    [JsonPropertyName("table")]
    public string Table { get; set; } = string.Empty;

    [JsonPropertyName("rows")]
    public List<SyncRowPayload> Rows { get; set; } = new();
}

public sealed class SyncRowPayload
{
    /// <summary>
    /// U=Upsert, D=Delete
    /// </summary>
    [JsonPropertyName("op")]
    public string Op { get; set; } = "U";

    /// <summary>
    /// 主鍵物件，例如 { "ID": "guid" }
    /// </summary>
    [JsonPropertyName("key")]
    public Dictionary<string, string?> Key { get; set; } = new();

    /// <summary>
    /// 欄位資料物件，例如 { "Category": "...", "FileName": "..."}
    /// Delete 時可為 null
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, string?>? Data { get; set; }
}