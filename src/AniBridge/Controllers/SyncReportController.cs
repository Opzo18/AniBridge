using AniBridge.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AniBridge.Controllers;

/// <summary>
/// Serves the last sync report to the settings page.
/// </summary>
[ApiController]
[Route("AniBridge")]
[Authorize]
public sealed class SyncReportController : ControllerBase
{
    private readonly ISyncReportStore _store;

    public SyncReportController(ISyncReportStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    [HttpGet("last-sync")]
    public ActionResult<SyncReport> GetLastSync()
    {
        var report = _store.Load();
        return report is null ? NotFound() : report;
    }

    [HttpGet("history")]
    public ActionResult<IReadOnlyList<SyncReport>> GetHistory()
    {
        return Ok(_store.LoadHistory());
    }
}
