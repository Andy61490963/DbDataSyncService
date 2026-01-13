using System.Globalization;

namespace DbDataSyncService.SyncB.Utilities;

/// <summary>
/// 同步資料欄位解析器（文化無關）。
/// </summary>
public static class SyncValueParser
{
    /// <summary>
    /// 解析必要字串欄位。
    /// </summary>
    public static string ParseRequiredString(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"欄位 {fieldName} 為必填");
        }

        return value;
    }

    /// <summary>
    /// 解析可為 null 的字串欄位（空白視為 null）。
    /// </summary>
    public static string? ParseOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// 解析 GUID 欄位。
    /// </summary>
    public static Guid ParseGuid(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"欄位 {fieldName} 格式不正確");
        }

        return parsed;
    }

    /// <summary>
    /// 解析布林欄位（支援 true/false/1/0）。
    /// </summary>
    public static bool ParseBool(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"欄位 {fieldName} 為必填");
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException($"欄位 {fieldName} 格式不正確");
    }

    /// <summary>
    /// 解析日期時間欄位（ISO 8601）。
    /// </summary>
    public static DateTime ParseDateTime(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"欄位 {fieldName} 為必填");
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed.UtcDateTime;
        }

        throw new InvalidOperationException($"欄位 {fieldName} 格式不正確");
    }
}
