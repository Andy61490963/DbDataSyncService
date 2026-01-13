using System.Globalization;

namespace DbDataSyncService.SyncA.Utilities;

/// <summary>
/// 將資料庫值轉為穩定格式字串（避免文化差異）。
/// </summary>
public static class InvariantValueConverter
{
    /// <summary>
    /// 將物件轉為文化無關的字串格式。
    /// </summary>
    public static string? ToInvariantString(object? value)
    {
        return value switch
        {
            null => null,
            Guid g => g.ToString("D"),
            DateTime dt => dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O")
                : dt.ToUniversalTime().ToString("O"),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("O"),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
