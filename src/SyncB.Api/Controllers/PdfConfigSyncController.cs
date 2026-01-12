using DbDataSyncService.SyncB.Data;
using DbDataSyncService.SyncB.Models;
using DbDataSyncService.SyncB.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DbDataSyncService.SyncB.Controllers;

[ApiController]
[Route("api/sync/pdf-config")]
public sealed class PdfConfigSyncController : ControllerBase
{
    private readonly SyncStateRepository _stateRepository;
    private readonly SyncApplyService _applyService;
    private readonly ILogger<PdfConfigSyncController> _logger;

    public PdfConfigSyncController(
        SyncStateRepository stateRepository,
        SyncApplyService applyService,
        ILogger<PdfConfigSyncController> logger)
    {
        _stateRepository = stateRepository;
        _applyService = applyService;
        _logger = logger;
    }

    /// <summary>
    /// 取得同步水位資訊。
    /// </summary>
    [HttpGet("state")]
    public async Task<ActionResult<SyncStateDto>> GetState([FromQuery] string syncKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(syncKey))
        {
            return BadRequest("syncKey 為必填");
        }

        var state = await _stateRepository.GetStateAsync(syncKey, cancellationToken);
        if (state is null)
        {
            return NotFound();
        }

        return Ok(state);
    }

    /// <summary>
    /// 套用同步變更。
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] SyncApplyRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("同步請求不可為空");
        }

        try
        {
            await _applyService.ApplyAsync(request, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "同步參數錯誤，SyncKey={SyncKey}", request.SyncKey);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步套用失敗，SyncKey={SyncKey}", request.SyncKey);
            return StatusCode(500, "同步套用失敗");
        }
    }
}
