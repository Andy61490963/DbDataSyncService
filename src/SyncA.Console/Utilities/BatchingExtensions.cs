namespace DbDataSyncService.SyncA.Utilities;

/// <summary>
/// 批次切割工具。
/// </summary>
public static class BatchingExtensions
{
    /// <summary>
    /// 將序列切割成固定大小的批次。
    /// </summary>
    public static IEnumerable<IReadOnlyList<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var bucket = new List<T>(batchSize);
        foreach (var item in source)
        {
            bucket.Add(item);
            if (bucket.Count == batchSize)
            {
                yield return bucket.ToList();
                bucket.Clear();
            }
        }

        if (bucket.Count > 0)
        {
            yield return bucket.ToList();
        }
    }
}
