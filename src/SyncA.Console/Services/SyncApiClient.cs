using System.Net.Http.Json;
using DbDataSyncService.SyncA.Configuration;
using DbDataSyncService.SyncA.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbDataSyncService.SyncA.Services;

/// <summary>
/// 封裝對 B 端同步 API 的呼叫。
/// </summary>
public sealed class SyncApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SyncJobOptions _options;
    private readonly ILogger<SyncApiClient> _logger;

    public SyncApiClient(HttpClient httpClient, IOptions<SyncJobOptions> options, ILogger<SyncApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.TargetApiBaseUrl);
    }

    /// <summary>
    /// 取得 B 端同步水位。
    /// </summary>
    public async Task<SyncStateResponse?> GetStateAsync(string syncKey, CancellationToken cancellationToken)
    {
        var url = $"/api/sync/pdf-config/state?syncKey={Uri.EscapeDataString(syncKey)}";
        return await _httpClient.GetFromJsonAsync<SyncStateResponse>(url, cancellationToken);
    }

    /// <summary>
    /// 套用同步資料至 B 端。
    /// </summary>
    public async Task ApplyAsync(SyncApplyRequest request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/sync/pdf-config/apply",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("同步套用失敗: {StatusCode} {Payload}", response.StatusCode, payload);
            response.EnsureSuccessStatusCode();
        }
    }
}
